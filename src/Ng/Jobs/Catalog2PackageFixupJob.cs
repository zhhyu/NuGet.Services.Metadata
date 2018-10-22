﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Packaging.Core;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGetGallery;

namespace Ng.Jobs
{
    public class Catalog2PackageFixupJob : LoopingNgJob
    {
        private const int MaximumPackageProcessingAttempts = 5;
        private static readonly TimeSpan MaximumPackageProcessingTime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan DonePollingInterval = TimeSpan.FromMinutes(1);

        private CatalogIndexReader _catalogIndexReader;
        private CloudBlobContainer _container;

        private Func<CatalogIndexEntry, Task> _handler;

        public Catalog2PackageFixupJob(ITelemetryService telemetryService, ILoggerFactory loggerFactory)
            : base(telemetryService, loggerFactory)
        {
            ThreadPool.SetMinThreads(MaxDegreeOfParallelism, 4);
        }

        public override string GetUsage()
        {
            return "Usage: ng catalog2packagefixup"
                   + $"-{Arguments.Source} <catalog>"
                   + $"-{Arguments.Verify} true/false"
                   + $"-{Arguments.StorageAccountName} <azure-account>"
                   + $"-{Arguments.StorageKeyValue} <azure-key> "
                   + $"-{Arguments.StorageContainer} <azure-container>";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            var storageAccount = arguments.GetOrThrow<string>(Arguments.StorageAccountName);
            var storageKey = arguments.GetOrThrow<string>(Arguments.StorageKeyValue);
            var storageContainer = arguments.GetOrThrow<string>(Arguments.StorageContainer);
            var verify = arguments.GetOrDefault(Arguments.Verify, false);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);

            // Prepare the catalog reader.
            var httpMessageHandlerFactory = CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, verbose);
            var collectorHttpClient = new CollectorHttpClient(httpMessageHandlerFactory());

            _catalogIndexReader = new CatalogIndexReader(new Uri(source), collectorHttpClient, TelemetryService);

            // Prepare the Azure Blob Storage client.
            var credentials = new StorageCredentials(storageAccount, storageKey);
            var account = new CloudStorageAccount(credentials, useHttps: true);

            _container = account
                .CreateCloudBlobClient()
                .GetContainerReference(storageContainer);

            // Prepare the handler that will run on each catalog entry.
            if (verify)
            {
                Logger.LogInformation("Validating that all packages have the proper Content MD5 hash...");
                _handler = ValidatePackage;
            }
            else
            {
                Logger.LogInformation("Ensuring all packages have a Content MD5 hash...");
                _handler = ProcessPackage;
            }
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Parsing catalog for all entries.");

            var entries = await _catalogIndexReader.GetEntries();

            var latestEntries = entries
                .GroupBy(c => new PackageIdentity(c.Id, c.Version))
                .Select(g => g.OrderByDescending(c => c.CommitTimeStamp).First())
                .Where(c => !c.IsDelete());

            var packageEntries = new ConcurrentBag<CatalogIndexEntry>(latestEntries);

            Logger.LogInformation("Processing packages.");

            var stopwatch = Stopwatch.StartNew();
            var processed = packageEntries.Count;

            var tasks = Enumerable
                .Range(0, ServicePointManager.DefaultConnectionLimit)
                .Select(async i =>
                {
                    while (packageEntries.TryTake(out var entry))
                    {
                        await _handler(entry);
                    }
                })
                .ToList();

            await Task.WhenAll(tasks);

            Logger.LogInformation(
                "Processed {ProcessedCount} packages in {ProcessDuration}",
                processed,
                stopwatch.Elapsed);
        }

        private async Task ValidatePackage(CatalogIndexEntry packageEntry)
        {
            try
            {
                var blob = _container.GetBlockBlobReference(BuildPackageFileName(packageEntry));

                await blob.FetchAttributesAsync();

                if (blob.Properties.ContentMD5 == null)
                {
                    Logger.LogError(
                        "Package {PackageId} {PackageVersion} has a null content MD5 hash!",
                        packageEntry.Id,
                        packageEntry.Version);
                    return;
                }

                string hash;
                using (var hashAlgorithm = MD5.Create())
                using (var packageStream = await blob.OpenReadAsync())
                {
                    var hashBytes = hashAlgorithm.ComputeHash(packageStream);
                    hash = Convert.ToBase64String(hashBytes);
                }

                if (blob.Properties.ContentMD5 != hash)
                {
                    Logger.LogError(
                        "Package {PackageId} {PackageVersion} has an incorrect content MD5 hash! Expected: '{ExpectedHash}', actual: '{ActualHash}'",
                        packageEntry.Id,
                        packageEntry.Version,
                        hash,
                        blob.Properties.ContentMD5);
                }
            }
            catch (StorageException e) when (IsPackageDoesNotExistException(e))
            {
                Logger.LogError(
                    "Package {PackageId} {PackageVersion} is missing from the packages container!",
                    packageEntry.Id,
                    packageEntry.Version);
            }
            catch (Exception e)
            {
                Logger.LogError(
                    0,
                    e,
                    "Could not validate package {PackageId} {PackageVersion}!",
                    packageEntry.Id,
                    packageEntry.Version);
            }
        }

        private async Task ProcessPackage(CatalogIndexEntry packageEntry)
        {
            try
            {
                var blob = _container.GetBlockBlobReference(BuildPackageFileName(packageEntry));

                for (int i = 0; i < MaximumPackageProcessingAttempts; i++)
                {
                    await blob.FetchAttributesAsync();

                    if (blob.Properties.ContentMD5 != null)
                    {
                        // Skip the package if it has a Content MD5 hash. Don't log to minimize noise.
                        return;
                    }

                    try
                    {
                        string hash;
                        using (var hashAlgorithm = MD5.Create())
                        using (var packageStream = await blob.OpenReadAsync())
                        {
                            var hashBytes = hashAlgorithm.ComputeHash(packageStream);
                            hash = Convert.ToBase64String(hashBytes);
                        }

                        blob.Properties.ContentMD5 = hash;

                        var condition = new AccessCondition { IfMatchETag = blob.Properties.ETag };
                        await blob.SetPropertiesAsync(
                            condition,
                            options: null,
                            operationContext: null);

                        Logger.LogWarning(
                            "Updated hash '{Hash}' for package {PackageId} {PackageVersion} with ETag {ETag}",
                            hash,
                            packageEntry.Id,
                            packageEntry.Version,
                            blob.Properties.ETag);
                        return;
                    }
                    catch (StorageException e) when (e.IsPreconditionFailedException())
                    {
                        Logger.LogError(
                            0,
                            e,
                            $"Updating hash for package {{PackageId}} {{PackageVersion}} failed. Attempt {{Attempt}} of {MaximumPackageProcessingAttempts}",
                            packageEntry.Id,
                            packageEntry.Version,
                            i + 1);
                    }
                }

                Logger.LogError(
                    $"Failed to update package {{PackageId}} {{PackageVersion}} after {MaximumPackageProcessingAttempts} attempts",
                    packageEntry.Id,
                    packageEntry.Version);
            }
            catch (StorageException e) when (IsPackageDoesNotExistException(e))
            {
                Logger.LogError(
                    "Package {PackageId} {PackageVersion} is missing from the packages container!",
                    packageEntry.Id,
                    packageEntry.Version);
            }
            catch (Exception e)
            {
                Logger.LogError(
                    0,
                    e,
                    "Could not process package {PackageId} {PackageVersion}",
                    packageEntry.Id,
                    packageEntry.Version);
            }
        }

        private bool IsPackageDoesNotExistException(StorageException e)
        {
            return e?.RequestInformation?.HttpStatusCode == (int?)HttpStatusCode.NotFound;
        }

        private string BuildPackageFileName(CatalogIndexEntry packageEntry)
        {
            return $"{packageEntry.Id.ToLower()}.{packageEntry.Version.ToNormalizedString().ToLower()}.nupkg";
        }
    }
}

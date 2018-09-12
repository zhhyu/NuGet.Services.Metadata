// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace NuGet.AzureSearch
{
    public class Db2AzureSearch
    {
        /// <summary>
        /// Package status key "Available" value.
        /// </summary>
        private const int Available = 0;

        private readonly string _connectionString;
        private readonly Uri _catalogIndexUrl;
        private readonly string _searchService;
        private readonly string _searchIndexName;
        private readonly string _searchApiKey;
        private readonly ILogger<Db2AzureSearch> _logger;

        public Db2AzureSearch(
            string connectionString,
            Uri catalogIndexUrl,
            string searchService,
            string searchIndexName,
            string searchApiKey,
            ILogger<Db2AzureSearch> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _catalogIndexUrl = catalogIndexUrl ?? throw new ArgumentNullException(nameof(catalogIndexUrl));
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _searchIndexName = searchIndexName ?? throw new ArgumentNullException(nameof(searchIndexName));
            _searchApiKey = searchApiKey ?? throw new ArgumentNullException(nameof(searchApiKey));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExportAsync()
        {
            var batches = new ConcurrentBag<List<object>>();

            var producerTask = ProducePackageBatchesAsync(batches);
        }

        private async Task ProducePackageBatchesAsync(ConcurrentBag<List<object>> batches)
        {
            // Get the commit timestamp from catalog index page for the initial cursor value, which will be picked up
            // by the incremental job.
            var initTime = await GetCommitTimestampFromCatalogAsync();

            // Create package registration key ranges so that each range has roughly N total package versions.
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Calculating package registration key ranges.");
            var keyRanges = await CalculateKeyRangesAsync();
            _logger.LogInformation("Calculated {BatchCount} ranges (took {Duration}).", keyRanges.Count, stopwatch.Elapsed);


        }

        private async Task<DateTime> GetCommitTimestampFromCatalogAsync()
        {
            using (var client = new HttpClient())
            using (var response = await client.GetAsync(_catalogIndexUrl))
            {
                _logger.LogInformation("Fetching catalog index page: {0}", response.StatusCode);
                response.EnsureSuccessStatusCode();

                string json = response.Content.ReadAsStringAsync().Result;
                JObject obj = JObject.Parse(json);
                return obj["commitTimeStamp"].ToObject<DateTime>();
            }
        }

        private async Task<List<PackageRegistrationKeyRange>> CalculateKeyRangesAsync()
        {
            var packageCounts = new List<PackageRegistrationKeyAndPackageCount>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var queryText = $@"
                    SELECT
                        pr.[Key],
                        COUNT(*) AS PackageCount
                    FROM PackageRegistrations pr
                    INNER JOIN Packages p ON p.PackageRegistrationKey = pr.[Key]
                    WHERE p.PackageStatusKey = @Available
                    GROUP BY pr.[Key]
                    ORDER BY pr.[Key]";
                using (var command = new SqlCommand(queryText, connection))
                {
                    command.CommandTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;
                    command.Parameters.AddWithValue("Available", Available);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            packageCounts.Add(new PackageRegistrationKeyAndPackageCount(
                                packageRegistrationKey: reader.GetInt32(0),
                                packageCount: reader.GetInt32(1)));
                        }
                    }
                }
            }

            return GroupKeyRanges(packageCounts);
        }

        public static List<PackageRegistrationKeyRange> GroupKeyRanges(List<PackageRegistrationKeyAndPackageCount> packageCounts)
        {
            const int maxBatchSize = 1000;

            // Batch the package registrations so that roughly N packages are included per batch. Batches may be larger
            // than N if a single package registration has more than N package versions.
            var batches = new List<PackageRegistrationKeyRange>();
            var beginKey = packageCounts[0].Key;
            var batchSize = 0;
            var endKey = 0;

            foreach (var current in packageCounts)
            {
                if (batchSize + current.PackageCount > maxBatchSize
                    && beginKey != current.Key)
                {
                    batches.Add(new PackageRegistrationKeyRange(beginKey, current.Key, batchSize));
                    beginKey = current.Key;
                    endKey = current.Key;
                    batchSize = current.PackageCount;
                }
                else
                {
                    endKey = current.Key;
                    batchSize += current.PackageCount;
                }
            }

            batches.Add(new PackageRegistrationKeyRange(beginKey, endKey + 1, batchSize));

            return batches;
        }

        private async Task<List<object>> GetPackageBatchAsync(PackageRegistrationKeyRange keyRange)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var queryText = @"
                    SELECT
                        p.[Key]                          'key',
                        pr.Id                            'id',
                        p.[Version]                      'verbatimVersion',
                        p.NormalizedVersion              'version',
                        p.Title                          'title',
                        p.Tags                           'tags',
                        p.[Description]                  'description',
                        p.DownloadCount                  'downloadCount',
                        p.FlattenedAuthors               'authors',
                        p.Summary                        'summary',
                        p.IconUrl                        'iconUrl',
                        p.ProjectUrl                     'projectUrl',
                        p.MinClientVersion               'minClientVersion',
                        p.ReleaseNotes                   'releaseNotes',
                        p.Copyright                      'copyright',
                        p.[Language]                     'language',
                        p.LicenseUrl                     'licenseUrl',
                        p.RequiresLicenseAcceptance      'requireLicenseAcceptance',
                        p.[Hash]                         'packageHash',
                        p.HashAlgorithm                  'packageHashAlgorithm',
                        p.PackageFileSize                'packageSize',
                        p.FlattenedDependencies          'flattenedDependencies',
                        pr.DownloadCount                 'totalDownloadCount',
                        pr.IsVerified                    'isVerified',
                        p.Created                        'created',
                        p.LastEdited                     'lastEdited',
                        p.Published                      'published',
                        p.Listed                         'listed',
                        p.SemVerLevelKey                 'semVerLevelKey'
                    FROM PackageRegistrations pr
                    INNER JOIN Packages p ON p.PackageRegistrationKey = pr.[Key]
                    WHERE p.PackageStatusKey = @Available
                      AND pr.[Key] >= @BeginKey
                      AND pr.[Key] < @EndKey";
                using (var command = new SqlCommand(queryText, connection))
                {
                    command.CommandTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds;
                    command.Parameters.AddWithValue("Available", Available);
                    command.Parameters.AddWithValue("BeginKey", keyRange.BeginKey);
                    command.Parameters.AddWithValue("EndKey", keyRange.EndKey);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var batch = new List<object>();

                        while (await reader.ReadAsync())
                        {
                        }

                        return batch;
                    }
                }
            }
        }

        public class PackageRegistrationKeyAndPackageCount
        {
            public PackageRegistrationKeyAndPackageCount(int packageRegistrationKey, int packageCount)
            {
                Key = packageRegistrationKey;
                PackageCount = packageCount;
            }

            public int Key { get; }
            public int PackageCount { get; }
        }

        public class PackageRegistrationKeyRange
        {
            public PackageRegistrationKeyRange(int beginKey, int endKey, int packageCount)
            {
                BeginKey = beginKey;
                EndKey = endKey;
                PackageCount = packageCount;
            }

            public int BeginKey { get; }
            public int EndKey { get; }
            public int PackageCount { get; }
        }
    }
}

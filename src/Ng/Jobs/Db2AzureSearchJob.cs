// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.AzureSearch;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;

namespace Ng.Jobs
{
    public class Db2AzureSearchJob : NgJob
    {
        private string _connectionString;
        private Uri _catalogIndexUrl;
        private string _searchService;
        private string _searchIndexName;
        private string _searchApiKey;

        public Db2AzureSearchJob(ITelemetryService telemetryService, ILoggerFactory loggerFactory) : base(telemetryService, loggerFactory)
        {
        }

        public override string GetUsage()
        {
            // TODO: add parameters for storage information. We need to write a cursor.json.
            return "Usage: ng db2azuresearch "
                   + $"-{Arguments.ConnectionString} <connection-string> "
                   + $"-{Arguments.Source} <catalog-source>"
                   + $"-{Arguments.SearchService} <search-service> "
                   + $"-{Arguments.SearchServiceName} <search-index-name> "
                   + $"-{Arguments.SearchApiKey} <search-api-key> "
                   + $"[-{Arguments.VaultName} <keyvault-name> "
                   + $" -{Arguments.ClientId} <keyvault-client-id> "
                   + $" -{Arguments.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                   + $" [-{Arguments.ValidateCertificate} true|false]] "
                   + $"[-{Arguments.Verbose} true|false]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            _connectionString = arguments.GetOrThrow<string>(Arguments.ConnectionString);
            _catalogIndexUrl = new Uri(arguments.GetOrThrow<string>(Arguments.Source));
            _searchService = arguments.GetOrThrow<string>(Arguments.SearchService);
            _searchIndexName = arguments.GetOrThrow<string>(Arguments.SearchServiceName);
            _searchApiKey = arguments.GetOrThrow<string>(Arguments.SearchApiKey);
        }
        
        protected override async Task RunInternal(CancellationToken cancellationToken)
        {
            var db2AzureSearch = new Db2AzureSearch(
                _connectionString,
                _catalogIndexUrl,
                _searchService,
                _searchIndexName,
                _searchApiKey,
                LoggerFactory.CreateLogger<Db2AzureSearch>());

            await db2AzureSearch.ExportAsync();
        }
    }
}

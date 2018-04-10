﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Microsoft.ApplicationInsights;

namespace NuGet.Services.Metadata.Catalog
{
    public class TelemetryService : ITelemetryService
    {
        private readonly TelemetryClient _telemetryClient;

        private const string HttpHeaderDurationSeconds = "HttpHeaderDurationSeconds";
        private const string Method = "Method";
        private const string Uri = "Uri";
        private const string Success = "Success";
        private const string StatusCode = "StatusCode";
        private const string ContentLength = "ContentLength";

        private const string CatalogIndexReadDurationSeconds = "CatalogIndexReadDurationSeconds";

        private const string CatalogIndexWriteDurationSeconds = "CatalogIndexWriteDurationSeconds";

        public TelemetryService(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackHttpHeaderDuration(
            TimeSpan duration,
            HttpMethod method,
            Uri uri,
            bool success,
            HttpStatusCode? statusCode,
            long? contentLength)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            _telemetryClient.TrackMetric(
                HttpHeaderDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { Method, method.ToString() },
                    { Uri, uri.AbsoluteUri },
                    { StatusCode, ((int?)statusCode)?.ToString() },
                    { Success, success.ToString() },
                    { ContentLength, contentLength?.ToString() }
                });
        }

        public void TrackCatalogIndexReadDuration(TimeSpan duration, Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            _telemetryClient.TrackMetric(
                CatalogIndexReadDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { Uri, uri.AbsoluteUri },
                });
        }

        public void TrackCatalogIndexWriteDuration(TimeSpan duration, Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            _telemetryClient.TrackMetric(
                CatalogIndexWriteDurationSeconds,
                duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    { Uri, uri.AbsoluteUri },
                });
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using VDS.RDF;

namespace Ng.Jobs
{
    public class RdfTool : NgJob
    {
        private const string NuspecFileArgument = "Nuspec";
        private const string NupkgFileArgument = "Nupkg";

        public RdfTool(ITelemetryService telemetryService, ILoggerFactory loggerFactory)
            : base(telemetryService, loggerFactory)
        {
            
        }
        
        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var nuspec = arguments.GetOrDefault<string>(NuspecFileArgument);
            var nupkg = arguments.GetOrDefault<string>(NupkgFileArgument);

            if (nuspec == null && nupkg == null)
            {
                throw new Exception("Need to specify either nuspec or nupkg");
            }
            if (nuspec != null && nupkg != null)
            {
                throw new Exception("Must specify only one input file");
            }
            var filename = nuspec ?? nupkg;

            if (!File.Exists(filename))
            {
                throw new Exception($"File {filename} does not seem to exist");
            }

            var baseFilename = Path.GetFileNameWithoutExtension(filename);

            XDocument nuspecXml = null;

            if (nuspec != null)
            {
                nuspecXml = LoadNuspec(nuspec);
            }
            else if (nupkg != null)
            {
                nuspecXml = LoadNuspecFromNupkg(nupkg);
            }

            const string BaseAddress = "https://www.nuget.org/data/";
            var rdfXml = Utils.GetRdfXml(nuspecXml, BaseAddress);

            var rdfXmlOutputFile = $"{baseFilename}-rdf.xml";

            SaveRdfXml(rdfXml, rdfXmlOutputFile);

            var graph = Utils.CreateNuspecGraph(nuspecXml, BaseAddress);

            var graphOutputFile = $"{baseFilename}.turtle";
            SaveTurtle(graph, graphOutputFile);
        }

        private void SaveTurtle(IGraph graph, string graphOutputFile)
        {
            using (var f = File.OpenWrite(graphOutputFile))
            using (var tw = new StreamWriter(f, Encoding.UTF8))
            {
                Utils.Dump(graph, tw);
            }
        }

        private void SaveRdfXml(XDocument rdfXml, string rdfXmlOutputFile)
        {
            using (var f = File.OpenWrite(rdfXmlOutputFile))
            using (var w = new XmlTextWriter(f, Encoding.UTF8))
            {
                w.Formatting = Formatting.Indented;
                rdfXml.Save(w);
            }
        }

        private XDocument LoadNuspecFromNupkg(string nupkg)
        {
            using (var f = File.OpenRead(nupkg))
            using (var pkg = Utils.GetPackage(f))
            {
                return Utils.GetNuspec(pkg);
            }
        }

        private XDocument LoadNuspec(string nuspec)
        {
            using (var f = File.OpenRead(nuspec))
            {
                return XDocument.Load(f);
            }
        }

        public override string GetUsage()
        {
            return $"Usage: ng rdftool (-{NuspecFileArgument} <nuspec file>|-{NupkgFileArgument} <nupkg>)";
        }

        protected override Task RunInternalAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}

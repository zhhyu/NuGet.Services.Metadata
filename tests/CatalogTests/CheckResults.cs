﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class CheckResults
    {
        public static void Test0()
        {
            JObject obj = JObject.Parse((new StreamReader(@"C:\data\site\test\resolver\test.metadata.service.json")).ReadToEnd());

            JArray packages = (JArray)obj["package"];

            Console.WriteLine(packages.Count);
        }
    }
}

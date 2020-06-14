using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;

namespace ConsoleAppBenchmark
{
    public class DictionaryRunner
    {
        [Params((StringComparison)(-1), StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase, StringComparison.InvariantCulture, StringComparison.InvariantCultureIgnoreCase)]
        public StringComparison Comparison { get; set; }

        private Dictionary<string, object> _dict;

        [GlobalSetup]
        public void Setup()
        {
            if (Comparison == (StringComparison)(-1))
            {
                _dict = new Dictionary<string, object>();
            }
            else
            {
                _dict = new Dictionary<string, object>(StringComparer.FromComparison(Comparison));
            }

            _dict["Some-Sample-Key"] = null;
        }

        [Benchmark]
        [Arguments("some-sample-key")]
        [Arguments("key-not-found")]
        public bool ContainsKey(string key) => _dict.ContainsKey(key);
    }
}

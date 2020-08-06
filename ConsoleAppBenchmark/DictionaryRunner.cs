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
        private HashSet<string> _set;

        [GlobalSetup]
        public void Setup()
        {
            if (Comparison == (StringComparison)(-1))
            {
                _dict = new Dictionary<string, object>();
                _set = new HashSet<string>();
            }
            else
            {
                _dict = new Dictionary<string, object>(StringComparer.FromComparison(Comparison));
                _set = new HashSet<string>(StringComparer.FromComparison(Comparison));
            }

            _dict["Some-Sample-Key"] = null;
            _set.Add("Some-Sample-Key");
        }

        [Benchmark]
        [Arguments("some-sample-key")]
        [Arguments("key-not-found")]
        public bool DictContainsKey(string key) => _dict.ContainsKey(key);

        [Benchmark]
        [Arguments("some-sample-key")]
        [Arguments("key-not-found")]
        public bool HashSetContainsKey(string key) => _set.Contains(key);
    }
}

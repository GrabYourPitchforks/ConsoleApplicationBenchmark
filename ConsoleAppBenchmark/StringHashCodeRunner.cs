using System;
using System.Collections.Generic;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    [MemoryDiagnoser]
    public class StringHashCodeRunner
    {
        private EqualityComparer<string> _equalityComparer;
        private string _str;

        [Params(0, 1, 2, /*3, 7, 16, 64,*/ 256)]
        public int StringLength;

        [GlobalSetup]
        public void Setup()
        {
            Type equalityComparerType = typeof(string).Assembly.GetType("System.Collections.Generic.NonRandomizedStringEqualityComparer");
            _equalityComparer = (EqualityComparer<string>)equalityComparerType.GetProperty("Default", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            _str = new string('x', StringLength);
        }

        [Benchmark]
        public new int GetHashCode()
        {
            return _equalityComparer.GetHashCode(_str);
        }
    }
}

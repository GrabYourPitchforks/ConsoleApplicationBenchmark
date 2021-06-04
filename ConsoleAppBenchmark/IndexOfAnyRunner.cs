using BenchmarkDotNet.Attributes;
using System;
using System.Globalization;
using System.Linq;

namespace ConsoleAppBenchmark
{
    public class IndexOfAnyRunner<T> where T : IEquatable<T>
    {
        [Params(4, 16, 64, 256, 1024, 4096)]
        public int HaystackLength;

        [Params(true, false)]
        public bool LastNeedleMatches;

        private T[] _haystack;
        private T[] _needles;

        [GlobalSetup]
        public void Setup()
        {
            _haystack = new T[HaystackLength];
            _needles = Enumerable.Range('a', 6).Select(el => (T)Convert.ChangeType(el, typeof(T), CultureInfo.InvariantCulture)).ToArray();
            T needle = (LastNeedleMatches) ? _needles.Last() : _needles.First();
            for (int i = 0; i < _haystack.Length; i += 4)
            {
                _haystack[i] = needle;
            }
        }

        [Benchmark]
        public int SliceInALoop()
        {
            var haystack = _haystack;
            _ = haystack.Length; // allow JIT to prove not null
            ReadOnlySpan<T> haystackSpan = haystack;

            var needles = _needles;
            _ = needles.Length; // allow JIT to prove not null
            ReadOnlySpan<T> needlesSpan = needles;

            while (true)
            {
                int idx = haystackSpan.IndexOfAny(needlesSpan);
                if (idx < 0)
                {
                    return haystackSpan.Length; // length of final slice
                }
                haystackSpan = haystackSpan.Slice(idx + 1);
            }
        }
    }
}

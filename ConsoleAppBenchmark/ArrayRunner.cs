using BenchmarkDotNet.Attributes;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ConsoleAppBenchmark
{
    public class ArrayRunner
    {
        private int[] _ints;

        [Params(0, 1, 4, 12, 24, 128)]
        public int ArrayLength { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _ints = Enumerable.Range(0, 32).ToArray();
        }

        [Benchmark]
        public int[] ArrResize()
        {
            int[] ints = _ints;
            Array.Resize(ref ints, ArrayLength);
            return ints;
        }

        [Benchmark]
        public uint[] ArrResizeCompatibleValueType()
        {
            uint[] uints = Unsafe.As<uint[]>(_ints); // safe, but avoid 'castclass' for benchmark
            Array.Resize(ref uints, ArrayLength);
            return uints;
        }
    }
}

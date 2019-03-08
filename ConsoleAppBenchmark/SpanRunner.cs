using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    public class SpanRunner
    {
        const int NUM_ITERS = 100_000;

        private int[] _ints;

        [Params(0, 12, 48, 512, 2048)]
        public int SpanLength;

        [GlobalSetup]
        public void Setup()
        {
            Random rnd = new Random(0x12345);

            _ints = new int[SpanLength];

            for (int i = 0; i < _ints.Length; i++)
            {
                _ints[i] = rnd.Next();
            }
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
        public int SumInts()
        {
            int[] intsArray = _ints;
            _ = intsArray.Length;
            ReadOnlySpan<int> ints = _ints;

            int retVal = default;

            for (int j = 0; j < NUM_ITERS; j++)
            {
                for (int i = 0; i < ints.Length; i++)
                {
                    retVal += ints[i];
                }
            }

            return retVal;
        }
    }
}

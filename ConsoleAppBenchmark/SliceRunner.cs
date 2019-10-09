using System;
using System.Collections.Generic;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    //[DisassemblyDiagnoser(recursiveDepth: 10)]
    public class SliceRunner
    {
        private Range _range = 4..8;

        [Benchmark(Baseline = true)]
        public ulong Slice()
        {
            ulong a = (uint)_range.Start.GetOffset(5);
            ulong b = (uint)_range.End.GetOffset(7);

            return a + b;
        }
    }
}

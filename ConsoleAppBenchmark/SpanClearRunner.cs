using System;
using System.Buffers.Text;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    public class SpanClearRunner
    {
        private readonly object[] _objs = new object[1024];

        // [Params(128)]
        [Params(0, 1, 3, 7, 16, 32, 128)]
        public int Length { get; set; }

        [Benchmark]
        public void Clear()
        {
            Span<object> span = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(_objs), Length);
            span.Clear();
        }
    }
}

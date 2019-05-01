using System;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    public class SpanCopyRunner
    {
        private byte[] _from4 = Enumerable.Range(0, 4).Select(i => (byte)i).ToArray();
        private byte[] _from1024 = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray();
        private byte[] _from1M = Enumerable.Range(0, 1000000).Select(i => (byte)i).ToArray();
        private byte[] _to = new byte[1000000];

        [Benchmark] public void CopySpan4() => _from4.AsSpan().CopyTo(_to);
        [Benchmark] public void CopySpan1024() => _from1024.AsSpan().CopyTo(_to);
        [Benchmark] public void CopySpan1M() => _from1M.AsSpan().CopyTo(_to);

    }
}

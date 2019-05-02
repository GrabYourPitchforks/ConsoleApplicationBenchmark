using BenchmarkDotNet.Attributes;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ConsoleAppBenchmark
{
    [MemoryDiagnoser]
    public class Utf8StringRunner
    {
        private string _str;
        private Utf8String _u8str;
        private byte[] _utf8Bytes;

        [Params(0, 8, 32, 128)]
        public int StringLength;

        [GlobalSetup]
        public void Setup()
        {
            _str = new string('x', StringLength);
            _u8str = new Utf8String(_str);
            _utf8Bytes = _u8str.ToByteArray();
        }

        [Benchmark(Baseline = true)]
        public int StringGetHashCode()
        {
            return _str.GetHashCode();
        }

        [Benchmark]
        public unsafe int Utf8StringGetHashCode()
        {
            return _u8str.GetHashCode();
        }
    }
}

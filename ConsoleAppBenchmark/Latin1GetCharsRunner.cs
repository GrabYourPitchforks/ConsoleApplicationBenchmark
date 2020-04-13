using BenchmarkDotNet.Attributes;
using System;
using System.Text;

namespace ConsoleAppBenchmark
{
    public class Latin1GetCharsRunner
    {
        private readonly Encoding _latin1Encoding = Encoding.GetEncoding("latin1");

        // [Params(0, 1, 7, 12, 84, 128, 4096)]
        [Params(4096)]
        public int Size;

        private byte[] _bytes;
        private char[] _chars = new char[4096];

        [GlobalSetup]
        public void Setup()
        {
            _bytes = new byte[Size];
            new Random(0xdead).NextBytes(_bytes);
        }

        [Benchmark]
        public int GetChars()
        {
            return _latin1Encoding.GetChars(_bytes, 0, Size, _chars, 0);
        }
    }
}

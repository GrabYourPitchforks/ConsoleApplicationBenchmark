using System;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    public class AsciiRunner
    {
        private const int ITER_COUNT = 100;

        private string _strAllAscii;
        private string _strWithNonAsciiData;

        [Params(0, 1, 2, 3, 4, 8, 12, 16, 32, 64, 128)]
        public int StringLength;

        [GlobalSetup]
        public void Setup()
        {
            _strAllAscii = new string('x', StringLength);
            _strWithNonAsciiData = _strAllAscii + "é" + _strAllAscii + "\U0010FFFF" + _strAllAscii;
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int GetBytes()
        {
            ReadOnlySpan<char> chars = _strAllAscii;
            Span<byte> bytes = stackalloc byte[128];

            int byteCount = 0;
            for (int i = 0; i < ITER_COUNT; i++)
            {
                byteCount = Encoding.ASCII.GetBytes(chars, bytes);
            }

            return byteCount;
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int GetBytes_WithFallback()
        {
            ReadOnlySpan<char> chars = _strWithNonAsciiData;
            Span<byte> bytes = stackalloc byte[512];

            int byteCount = 0;
            for (int i = 0; i < ITER_COUNT; i++)
            {
                byteCount = Encoding.ASCII.GetBytes(chars, bytes);
            }

            return byteCount;
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int GetByteCount()
        {
            string str = _strAllAscii;
            _ = str.Length; // null check

            int byteCount = 0;
            for (int i = 0; i < ITER_COUNT; i++)
            {
                byteCount = Encoding.ASCII.GetByteCount(str);
            }

            return byteCount;
        }
    }
}

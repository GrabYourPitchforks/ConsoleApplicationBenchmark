using BenchmarkDotNet.Attributes;
using System;
using System.Text;

namespace ConsoleAppBenchmark
{
    public class Latin1GetBytesRunner
    {
        private readonly Encoding _latin1Encoding = Encoding.GetEncoding("latin1");

        [Params(0, 1, 7, 12, 84, 128, 4096)]
        public int Size;

        private byte[] _bytes = new byte[4096];
        private char[] _charsAllLatin1;

        [GlobalSetup]
        public void Setup()
        {
            Random rnd = new Random(0x1a2b3c);

            _charsAllLatin1 = new char[Size];
            for (int i = 0; i < _charsAllLatin1.Length; i++)
            {
                _charsAllLatin1[i] = (char)(byte)rnd.Next();
            }
        }

        [Benchmark]
        public int GetBytes()
        {
            return _latin1Encoding.GetBytes(_charsAllLatin1, 0, Size, _bytes, 0);
        }
    }
}

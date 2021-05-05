using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Unicode;

namespace ConsoleAppBenchmark
{
    public class StrCpyRunner
    {
        private char[] _chars;

        [Params(4, 64, 800, 1024, 2048, 4096)]
        public int CharArrayLength;

        [GlobalSetup]
        public void Setup()
        {
            _chars = new char[CharArrayLength];
        }

        [Benchmark]
        public object MakeString()
        {
            // _ = new object();
            return new string(_chars);
        }
    }
}

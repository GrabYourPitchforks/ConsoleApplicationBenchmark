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
        private string _str;

        [Params(16, 71)]
        public int CharArrayLength;

        [GlobalSetup]
        public void Setup()
        {
            _chars = new char[CharArrayLength];
            _str = new string(_chars);
        }

        [Benchmark]
        public string Substr() => _str.Substring(2);
    }
}

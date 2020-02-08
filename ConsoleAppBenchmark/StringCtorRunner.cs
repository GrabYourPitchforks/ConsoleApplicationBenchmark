using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    //[MemoryDiagnoser]
    [DisassemblyDiagnoser(recursiveDepth: 3)]
    public class StringCtorRunner
    {
        private readonly char[] _chars = new char[] { 'h', 'e', 'l', 'l', 'o' };

        //[Benchmark]
        //public string StringCtorFromCharArray()
        //{
        //    return new string(_chars);
        //}

        [Benchmark]
        public string SpanOfCharToString()
        {
            char[] chars = _chars;
            _ = chars.Length;
            return new ReadOnlySpan<char>(chars).ToString();
        }
    }
}

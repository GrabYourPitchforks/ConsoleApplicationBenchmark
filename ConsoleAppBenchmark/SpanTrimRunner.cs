using System;
using System.Collections.Generic;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    [DisassemblyDiagnoser]
    public class SpanTrimRunner
    {
        [ParamsSource(nameof(TrimArguments))]
        public string Arg;

        [Benchmark]
        public ReadOnlySpan<char> Trim()
        {
            string arg = Arg;
            _ = arg.Length;
            return arg.AsSpan().Trim();
        }

        public static IEnumerable<object> TrimArguments()
        {
            yield return "";
            yield return " abcdefg ";
            yield return "abcdefg";
        }
    }
}

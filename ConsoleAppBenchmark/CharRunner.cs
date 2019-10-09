using System;
using System.Collections.Generic;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    [DisassemblyDiagnoser(recursiveDepth: 10)]
    public class CharRunner
    {
        [Benchmark]
        [ArgumentsSource(nameof(TrimArguments))]
        public bool IsNullOrWhiteSpace(string input) => string.IsNullOrWhiteSpace(input);

        public static IEnumerable<object> TrimArguments()
        {
          //  yield return "";
            yield return "你好";
          //  yield return "Κρόνος";
        }
    }
}

using BenchmarkDotNet.Attributes;
using System.Collections.Generic;

namespace ConsoleAppBenchmark
{
    [DisassemblyDiagnoser]
    public class CharRunner
    {
        [Benchmark]
        [ArgumentsSource(nameof(TrimArguments))]
        public bool IsNullOrWhiteSpace(string input) => string.IsNullOrWhiteSpace(input);

        public static IEnumerable<object> TrimArguments()
        {
            yield return "";
            yield return "你好";
            yield return "Κρόνος";
            yield return " banana ";
        }
    }
}

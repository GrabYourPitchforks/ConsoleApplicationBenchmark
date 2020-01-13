using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ConsoleAppBenchmark
{
    [DisassemblyDiagnoser(recursiveDepth: 10)]
    public class RegexRunner
    {
        private readonly Regex _regex = new Regex(@"\w+");

        [Benchmark]
        [ArgumentsSource(nameof(IsMatchArguments))]
        public bool IsMatch(string input) => _regex.IsMatch(input);

        public static IEnumerable<object> IsMatchArguments()
        {
            yield return "";
            yield return "你好";
            yield return "Κρόνος";
            yield return " banana ";
        }
    }
}

using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ConsoleAppBenchmark
{
    [DisassemblyDiagnoser]
    public partial class RegexRunner
    {
        private readonly Regex _regex = new Regex(@"\w+");

        [RegexGenerator("[A-Fa-f0-9]{8}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{12}", RegexOptions.ExplicitCapture)]
        private static partial Regex MyRegex();

        [Benchmark]
        [ArgumentsSource(nameof(IsMatchArguments))]
        public bool IsMatch(string input)
        {
            // if (Debugger.IsAttached) { Debugger.Break(); }
            return MyRegex().IsMatch(input);
        }

        public static IEnumerable<object> IsMatchArguments()
        {
            // yield return "";
            yield return "db9119ab-b0be-41cc-98b8-825374117ace";
            //yield return "Bees!";
            //yield return "είναι";
        }
    }
}

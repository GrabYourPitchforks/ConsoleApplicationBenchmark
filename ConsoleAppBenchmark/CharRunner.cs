using BenchmarkDotNet.Attributes;
using System.Collections.Generic;

namespace ConsoleAppBenchmark
{
    [DisassemblyDiagnoser]
    public class CharRunner
    {
        [Benchmark]
        [Arguments("Hello World!")]
        public int CharToUpperInvariant(string args)
        {
            int accum = 0;
            for (int i = 0; i < args.Length; i++)
            {
                accum += char.ToUpperInvariant(args[i]);
            }
            return accum;
        }

        [Benchmark]
        [Arguments("Hello World!")]
        public int CharToLowerInvariant(string args)
        {
            int accum = 0;
            for (int i = 0; i < args.Length; i++)
            {
                accum += char.ToLowerInvariant(args[i]);
            }
            return accum;
        }

        // [Benchmark]
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

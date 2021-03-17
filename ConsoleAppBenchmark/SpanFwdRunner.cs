using BenchmarkDotNet.Attributes;
using System;
using System.Runtime.CompilerServices;

namespace ConsoleAppBenchmark
{
    [DisassemblyDiagnoser]
    public class SpanFwdRunner
    {
        private readonly int[] _ints = new int[] { 1, 2, 3 };

        [Benchmark(Baseline = true)]
        [Arguments(2)]
        public bool Control(int value)
        {
            Span<int> span = _ints;
            return span.Contains(value);
        }

        [Benchmark(Baseline = false)]
        [Arguments(2)]
        public bool Variable(int value)
        {
            Span<int> span = _ints;
            return span.ContainsEx(value);
        }
    }

    internal static class Extensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsEx<T>(this Span<T> span, T value) where T : IEquatable<T>
        {
            return ((ReadOnlySpan<T>)span).Contains(value);
        }
    }
}

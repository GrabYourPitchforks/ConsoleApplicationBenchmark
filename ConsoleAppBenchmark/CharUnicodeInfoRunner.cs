using BenchmarkDotNet.Attributes;
using System.Globalization;

namespace ConsoleAppBenchmark
{
    public class CharUnicodeInfoRunner
    {
        [Params(0x0000)]
        public int Value;

        [Benchmark]
        public int GetDecimalDigitValue()
        {
            return CharUnicodeInfo.GetDecimalDigitValue((char)Value);
        }

        //[Benchmark]
        //public UnicodeCategory GetUnicodeCategory()
        //{
        //    return CharUnicodeInfo.GetUnicodeCategory((char)Value);
        //}
    }
}

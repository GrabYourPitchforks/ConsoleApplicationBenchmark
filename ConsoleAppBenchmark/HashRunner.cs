using System;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    public class HashRunner
    {
        const int ITER_COUNT = 100_000;

        [Params("0 - 8", "6 - 24", "20 - 96", "64 - 256")]
        public string InputLength;

        private string[] _strings;

        [GlobalSetup]
        public void Setup()
        {
            Random r = new Random(0x12345);

            string[] inputLengths = InputLength.Split(" - ");
            int minInclusive = int.Parse(inputLengths[0]);
            int maxExclusive = int.Parse(inputLengths[1]) + 1;

            string[] strings = new string[50_000];
            for (int i = 0; i < strings.Length; i++)
            {
                strings[i] = new string('\u1234', r.Next(minInclusive, maxExclusive));
            }

            _strings = strings;
        }

        [Benchmark]
        public int StringGetHashCode()
        {
            int retVal = default;

            string[] strings = _strings;
            for (int i = 0; i < strings.Length; i++)
            {
                retVal ^= strings[i].GetHashCode();
            }

            return retVal;
        }
    }
}

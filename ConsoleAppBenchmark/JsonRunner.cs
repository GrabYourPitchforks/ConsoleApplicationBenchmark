using BenchmarkDotNet.Attributes;
using System.Diagnostics;
using System.Text;

namespace ConsoleAppBenchmark
{
    public class JsonRunner
    {
        private const int ITER_COUNT = 10_000;

        private byte[] _jsonData;

        [GlobalSetup]
        public void Setup()
        {
            _jsonData = Encoding.UTF8.GetBytes(new string('x', 2048));
        }


        //[Benchmark(Baseline = true)]
        public int NoVector()
        {
            byte[] data = _jsonData;
            _ = _jsonData.Length; // tell runtime this is not null

            int retVal = default;
            for (int i = 0; i < ITER_COUNT; i++)
            {
                retVal = JsonOld.GetIndexOfFirstByteToEncode(data);
            }
            return retVal;
        }

        [Benchmark]
        public int WithVector()
        {
            //Debugger.Launch();
            //Debugger.Break();

            byte[] data = _jsonData;
            _ = _jsonData.Length; // tell runtime this is not null

            int retVal = default;
            for (int i = 0; i < ITER_COUNT; i++)
            {
                retVal = JsonNew.GetIndexOfFirstByteToEncode(data);
            }
            return retVal;
        }
    }
}

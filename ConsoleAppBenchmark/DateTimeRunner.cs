using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleAppBenchmark
{
    public class DateTimeRunner
    {
        [Benchmark]
        public DateTime GetUtcNow()
        {
            return DateTime.UtcNow;
        }
    }
}

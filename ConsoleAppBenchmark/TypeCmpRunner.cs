using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Unicode;

namespace ConsoleAppBenchmark
{
    public class TypeCmpRunner
    {
        private readonly Type _it = typeof(int);

        [Benchmark]
        [Arguments(typeof(int))]
        public bool TypeEqualityOp(Type t)
        {
            return t == _it;
        }
    }
}

using BenchmarkDotNet.Attributes;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace ConsoleAppBenchmark
{
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    public class FieldInfoLookupRunner
    {
        [Params("Field01", "Field07")]
        public string FieldName { get; set; }

        private RuntimeFieldHandle _fieldHandle;

        [GlobalSetup]
        public void Setup()
        {
            _ = typeof(MyClass).GetFields(); // populate internal cache
            _fieldHandle = typeof(MyClass).GetField(FieldName).FieldHandle;
        }

        [Benchmark]
        public FieldInfo GetFieldFromHandle()
        {
            return FieldInfo.GetFieldFromHandle(_fieldHandle);
        }

        [Benchmark]
        public ParallelLoopResult GetFieldFromHandleWithConcurrency()
        {
            return Parallel.For(0, 10000, _ =>
            {
                GetFieldFromHandle();
            });
        }

        public class MyClass
        {
            public string Field00;
            public string Field01;
            public string Field02;
            public string Field03;
            public string Field04;
            public string Field05;
            public string Field06;
            public string Field07;
            public string Field08;
        }
    }
}

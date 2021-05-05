using BenchmarkDotNet.Attributes;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConsoleAppBenchmark
{
    public unsafe class ArrayRunner
    {
        // [Params(4096)]
        [Params(0, 16, 64, 1024, 4096)]
        public int ArrLen { get; set; }

        private byte[] _arr;

        private delegate* managed<Array, int, int, void> _calli1;
        private delegate* managed<Array, void> _calli2;

        [GlobalSetup]
        public void Setup()
        {
            _arr = new byte[ArrLen];

            _calli1 = &Array.Clear;

            var mi = typeof(Array).GetMethod("Clear", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(Array) }, null);
            // if (mi is not null)
            {
                for (int i = 0; i < 1000; i++)
                    mi.Invoke(null, new object[] { new object[0] }); // ensure JITted

                _calli2 = (delegate* managed<Array, void>)mi.MethodHandle.GetFunctionPointer();
            }
        }

        //[Benchmark]
        //public void DoIt()
        //{
        //    int len = _arr.Length;
        //    Unsafe.InitBlockUnaligned(ref MemoryMarshal.GetArrayDataReference(_arr), 0, (uint)len);
        //}

        //[Benchmark]
        //public void Clear()
        //{
        //    byte[] arr = _arr;
        //    Array.Clear(length: arr.Length, array: arr, index: 0);
        //}

        //[Benchmark]
        //public void Clear_Old()
        //{
        //    _calli1(_arr, 0, _arr.Length);
        //}

        [Benchmark]
        public void Clear_New()
        {
            _calli2(_arr);
        }

    }
}

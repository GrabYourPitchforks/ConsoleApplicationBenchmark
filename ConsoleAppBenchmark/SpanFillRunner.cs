using BenchmarkDotNet.Attributes;
using System;

namespace ConsoleAppBenchmark
{
    // [GenericTypeArguments(typeof(byte))]
    [GenericTypeArguments(typeof(char))]
    //[GenericTypeArguments(typeof(int))]
    //[GenericTypeArguments(typeof(long))]
    // [GenericTypeArguments(typeof(float))]
    //[GenericTypeArguments(typeof(double))]
    //[GenericTypeArguments(typeof(decimal))]
    // [GenericTypeArguments(typeof(string))]
    public class SpanFillRunner<T>
    {
        private T[] _arr;

        private T _value;

        [Params(0)]
        public int Size;

        [GlobalSetup]
        public void Setup()
        {
            _arr = new T[Size];

            _value = (T)((IConvertible)42).ToType(typeof(T), null);
        }

        [Benchmark]
        public void Fill()
        {
            var arr = _arr;
            _ = arr.Length; // prove not null
            arr.AsSpan().Fill(_value);
        }
    }
}

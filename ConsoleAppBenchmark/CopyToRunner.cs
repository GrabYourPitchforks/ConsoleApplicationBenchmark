using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleAppBenchmark
{
    public class CopyToRunner<T>
    {
        [Params(2048)]
        public int Size;

        private T[] _array;
        private List<T> _list;
        private T[] _destination;

        [GlobalSetup]
        public void Setup()
        {
            if (typeof(T) == typeof(int))
            {
                _array = (T[])(object)Enumerable.Range(0, Size).ToArray();
            }
            else if (typeof(T) == typeof(string))
            {
                _array = (T[])(object)Enumerable.Range(0, Size).Select(val => val.ToString()).ToArray();
            }
            _list = new List<T>(_array);
            _destination = new T[Size];
        }

        [Benchmark(Baseline = true)]
        public void Array() => System.Array.Copy(_array, _destination, Size);

        [Benchmark]
        public void Span() => new Span<T>(_array).CopyTo(new Span<T>(_destination));
    }

    public class CopyToRunner
    {
        [Params(2048)]
        public int Size;

        private string[] _array;
        private List<string> _list;
        private string[] _destination;

        [GlobalSetup]
        public void Setup()
        {
            _array = Enumerable.Range(0, Size).Select(val => val.ToString()).ToArray();
            _list = new List<string>(_array);
            _destination = new string[Size];
        }

        //[Benchmark]
        //public void Array() => System.Array.Copy(_array, _destination, Size);

        [Benchmark]
        public void Span() => new Span<string>(_array).CopyTo(new Span<string>(_destination));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    public class ListRunner
    {
        const int ITER_COUNT = 10_000;

        private int _int = 42;
        private string _str = "hello!";
        private object _obj = new object();
        private List<object> _listObj;
        private List<int> _listInt;
        private List<int> _prePopListInt;

        [GlobalSetup]
        public void Setup()
        {
            _listObj = new List<object>();
            _listInt = new List<int>();
            _prePopListInt = new List<int>(Enumerable.Repeat(1234, ITER_COUNT));
        }

        [Benchmark]
        public int AddObjToObjList()
        {
            object obj = _obj;
            List<object> list = _listObj;
            list.Clear();

            for (int i = 0; i < ITER_COUNT; i++)
            {
                list.Add(obj);
            }

            return list.Count;
        }

        [Benchmark]
        public int AddStringToObjList()
        {
            object obj = _str;
            List<object> list = _listObj;
            list.Clear();

            for (int i = 0; i < ITER_COUNT; i++)
            {
                list.Add(obj);
            }

            return list.Count;
        }

        [Benchmark]
        public int AddIntToIntList()
        {
            int obj = _int;
            List<int> list = _listInt;
            list.Clear();

            for (int i = 0; i < ITER_COUNT; i++)
            {
                list.Add(obj);
            }

            return list.Count;
        }

        [Benchmark]
        public int SumInts()
        {
            List<int> list = _prePopListInt;
            list.GetType(); // null check

            int retVal = default;

            for (int i = 0; i < list.Count; i++)
            {
                retVal += list[i];
            }

            return retVal;
        }
    }
}

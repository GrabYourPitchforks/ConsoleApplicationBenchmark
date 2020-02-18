using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Microsoft.VisualBasic.CompilerServices;

namespace ConsoleAppBenchmark
{
    //[MemoryDiagnoser]
    [DisassemblyDiagnoser(recursiveDepth: 3)]
    public class ObjectFactoryRunner
    {
        private Func<object> _dmFactory;
        private Func<object> _objFactory;
        private Func<object> _doNothing;

        [GlobalSetup]
        public void Setup()
        {
            _dmFactory = CreateDynamicMethodFactory<object>();

            var t = typeof(object).Assembly.GetType("System.Reflection.ObjectFactory`1");
            t = t.MakeGenericType(typeof(object));
            var mi = t.GetMethod("CreateObject");
            var del = mi.CreateDelegate(typeof(Func<object>), Activator.CreateInstance(t));

            _objFactory = (Func<object>)del;

            _doNothing = CreateDoNothingFactory();
        }

        [Benchmark(Baseline = true)]
        public object DirectNewobj()
        {
            return new object();
        }

        [Benchmark]
        public object DynamicMethodNewobj()
        {
            return _dmFactory();
        }

        [Benchmark]
        public object ActivatorCreateInstance_Old()
        {
            return Activator.CreateInstance(typeof(object));
        }

        [Benchmark]
        public object ActivatorCreateInstance_New()
        {
            return _objFactory();
        }

        [Benchmark]
        public object DoNothing()
        {
            return _doNothing();
        }

        private static Func<T> CreateDynamicMethodFactory<T>() where T : new()
        {
            var dm = new DynamicMethod("factory", typeof(T), Type.EmptyTypes);
            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Newobj, typeof(T).GetConstructor(Type.EmptyTypes));
            il.Emit(OpCodes.Ret);
            return (Func<T>)dm.CreateDelegate(typeof(Func<T>));
        }

        private static Func<object> CreateDoNothingFactory()
        {
            var dm = new DynamicMethod("factory", typeof(object), Type.EmptyTypes);
            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ret);
            return (Func<object>)dm.CreateDelegate(typeof(Func<object>));
        }
    }
}

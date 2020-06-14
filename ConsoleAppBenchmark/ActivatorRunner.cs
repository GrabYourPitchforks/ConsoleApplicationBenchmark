using BenchmarkDotNet.Attributes;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ConsoleAppBenchmark
{
    // [MemoryDiagnoser]
    public class ActivatorRunner
    {
        private readonly Type _myType = typeof(MyClass);
        private Func<object> _fnFactory;

        [GlobalSetup]
        public void Setup()
        {
            for (int i = 0; i < 1000; i++)
            {
                new MyClass();
            }

            // Create factory

            var mi = typeof(Activator).GetMethod("CreateFactory", 1, Type.EmptyTypes );
            if (mi != null)
            {
                //var del = (Func<Type, bool, object>)Delegate.CreateDelegate(typeof(Func<Type, bool, object>), mi);
                //_fnMakeFact = () => del(typeof(MyClass), false);
                // throw new Exception("MI IS NULL");
                _fnFactory = (Func<object>)mi.MakeGenericMethod(typeof(MyClass)).Invoke(null, null);
            }
            else
            {
                //_fnMakeFact = () =>
                //{
                //    DynamicMethod dm = new DynamicMethod("MyMethod", typeof(object), Type.EmptyTypes);
                //    var ilg = dm.GetILGenerator();
                //    ilg.Emit(OpCodes.Newobj, typeof(MyClass).GetConstructor(Type.EmptyTypes));
                //    ilg.Emit(OpCodes.Ret);
                //    return dm.CreateDelegate(typeof(Func<object>));
                //};
                //throw new Exception("MI IS NOT NULL");
                Expression<Func<object>> expr = () => new MyClass();
                _fnFactory = expr.Compile();
            }
        }

        [Benchmark]
        public object ActivatorCreateInstance()
            => Activator.CreateInstance(_myType);

        [Benchmark]
        public object CreateInstanceFromFactory()
            => _fnFactory();


        private sealed class MyClass
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public MyClass() { }
        }
    }
}

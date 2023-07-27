using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    // [MemoryDiagnoser]
    public class ActivatorRunner
    {
        [Params(typeof(object), typeof(MyClass), typeof(List<int>), typeof(StringBuilder))]
        public Type Type { get; set; }

        private Func<object> _dmFactory;
        private Func<object> _objFactory;

        [GlobalSetup]
        public void Setup()
        {
            var dm = new DynamicMethod("Anonymous", typeof(object), Type.EmptyTypes);
            var ilgen = dm.GetILGenerator();

            ilgen.Emit(OpCodes.Newobj, Type.GetConstructor(Type.EmptyTypes));
            ilgen.Emit(OpCodes.Ret);

            _dmFactory = dm.CreateDelegate<Func<object>>();

            Activator.CreateInstance(Type); // prime the cache
            var pi = Type.GetType().GetProperty("GenericCache", BindingFlags.NonPublic | BindingFlags.Instance);
            object internalCache = pi.GetValue(Type);

            if (internalCache.GetType().Name != "ActivatorCache")
            {
                throw new Exception("Unknown cache object!");
            }

            var cache = Unsafe.As<ActivatorCache>(internalCache);
            _objFactory = cache.CreateInstance;
        }

        //[Benchmark(Baseline = true)]
        //public object UsingDynamicMethod()
        //    => _dmFactory();

        //[Benchmark]
        //public object ActivatorCreateInstance()
        //    => Activator.CreateInstance(Type);

        [Benchmark()]
        public object UsingObjectFactoryMethod()
            => _objFactory();

        private sealed class MyClass
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public MyClass() { }
        }

        private sealed unsafe class ActivatorCache
        {
            // The managed calli to the newobj routine, plus its first argument.
            // First argument is normally a MethodTable* unless we're going through one of our stubs.
            private readonly delegate*<IntPtr, object> _pfnNewobj;
            private readonly IntPtr _newobjState;

            // The managed calli to the parameterless ctor, plus a state object.
            // State object depends on the stub being called.
            private readonly delegate*<object, IntPtr, void> _pfnCtorStub;
            private readonly IntPtr _ctorStubState;

            public object CreateInstance()
            {
                object retVal = _pfnNewobj(_newobjState);
                _pfnCtorStub(retVal, _ctorStubState);
                return retVal;
            }
        }
    }
}

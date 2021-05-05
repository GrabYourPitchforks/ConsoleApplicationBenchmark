using BenchmarkDotNet.Attributes;
using System.Linq;
using System.Reflection;

namespace ConsoleAppBenchmark
{
    [MemoryDiagnoser]
    public class MethodInfoRunner
    {
        [Params(0, 1, 4, 5)]
        public int ArgCount { get; set; }

        private MethodInfo _mi;
        private ConstructorInfo _ci;
        private object[] _args;

        [GlobalSetup]
        public void Setup()
        {
            _args = Enumerable.Repeat((object)42, ArgCount).ToArray();
            _mi = typeof(MethodInfoRunner).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly).Single(mi => mi.GetParameters().Length == ArgCount);
            _ci = typeof(MyClass).GetConstructors(BindingFlags.Instance | BindingFlags.Public).Single(mi => mi.GetParameters().Length == ArgCount);
        }

        //[Benchmark]
        //public object ConstructorInfoInvoke() => _ci.Invoke(_args);

        [Benchmark]
        public object MethodInfoInvoke() => _mi.Invoke(null, _args);

        public static void Method0() { }
        public static void Method1(int p1) { }
        public static void Method4(int p1, int p2, int p3, int p4) { }
        public static void Method5(int p1, int p2, int p3, int p4, int p5) { }
        public static void Method8(int p1, int p2, int p3, int p4, int p5, int p6, int p7, int p8) { }
        public static void Method10(int p1, int p2, int p3, int p4, int p5, int p6, int p7, int p8, int p9, int p10) { }

        public class MyClass
        {
            public MyClass() { }
            public MyClass(int p1) { }
            public MyClass(int p1, int p2, int p3, int p4) { }
            public MyClass(int p1, int p2, int p3, int p4, int p5) { }
            public MyClass(int p1, int p2, int p3, int p4, int p5, int p6, int p7, int p8) { }
            public MyClass(int p1, int p2, int p3, int p4, int p5, int p6, int p7, int p8, int p9, int p10) { }
        }
    }
}

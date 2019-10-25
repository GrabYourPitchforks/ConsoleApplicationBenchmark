using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConsoleAppBenchmark
{
    public class BitManipulaitonRunner
    {
        private string _str;

        [GlobalSetup]
        public void Setup()
        {
            Random r = new Random(0x98147);

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < 256; i++)
            {
                int next = r.Next(0, 16);
                if (next < 10)
                {
                    builder.Append((char)(next + '0'));
                }
                else
                {
                    if (r.Next() % 2 != 0)
                    {
                        builder.Append((char)(next + 'A' - 10));
                    }
                    else
                    {
                        builder.Append((char)(next + 'a' - 10));
                    }
                }
            }

            _str = builder.ToString();
            Console.WriteLine(_str);
        }

        [Benchmark]
        public bool TestIsAllHexA()
        {
            foreach (char ch in _str)
            {
                if (!IsHexA(ch)) { return false; }
            }

            return true;
        }

        [Benchmark]
        public bool TestIsAllHexB()
        {
            foreach (char ch in _str)
            {
                if (!IsHexB(ch)) { return false; }
            }

            return true;
        }

        //[Benchmark]
        //public bool TestIsAllHexC()
        //{
        //    foreach (char ch in _str)
        //    {
        //        if (!IsHexC(ch)) { return false; }
        //    }

        //    return true;
        //}

        //[Benchmark]
        //public bool TestIsAllHexD()
        //{
        //    foreach (char ch in _str)
        //    {
        //        if (!IsHexD(ch)) { return false; }
        //    }

        //    return true;
        //}

        [Benchmark]
        public bool TestIsAllHexE()
        {
            foreach (char ch in _str)
            {
                if (!IsHexE(ch)) { return false; }
            }

            return true;
        }

        [Benchmark]
        public bool TestIsAllHexF()
        {
            foreach (char ch in _str)
            {
                if (!IsHexF(ch)) { return false; }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHexA(char i)
        {
            uint mask = (uint)(((int)i - 'A') >> 31) & 0x11;

            uint xx = (uint)i - 'A';
            xx += mask;
            xx &= ~0x20u;

            uint cmp = 6 + (((uint)i & 0x10) >> 2); // 6 or 10
            return xx < cmp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHexB(char i)
        {
            return ((uint)i - '0' < 10)
                || ((((uint)i - 'A') & ~0x20u) < 6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHexC(char i)
        {
            return ((uint)i - '0' < 10)
                | ((((uint)i - 'A') & ~0x20u) < 6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHexD(char i)
        {
            int a = ((i - '0') & 0xFFFF) - 10;
            int b = ((i - 'A') & 0xFFDF) - 6;
            return (a | b) < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHexE(char i)
        {
            uint value = i;
            uint a = ((value - 'A') & 0xFFDF) - 6;
            uint b = (value ^ 0x30) - 10;

            return (int)(a | b) < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHexF(char i)
        {
            uint value = i;
            uint a = ((value - 'A') & 0xFFDF);// - 6;
            uint b = (value ^ 0x30);// - 10;

            return (((int)a - 6) | ((int)b - 10)) < 0;
        }
    }
}

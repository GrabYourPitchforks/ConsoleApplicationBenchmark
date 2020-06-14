using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    [DisassemblyDiagnoser]
    public class HexRunner
    {
        private char[] _buffer = new char[8];

        [Arguments((uint)0xdeadbeef)]
        [Benchmark]
        public void UseSse3_Unsafe(uint value)
        {
            char[] buffer = _buffer;
            _ = buffer.Length; // elide future null checks
                               // _ = buffer[7]; // elide future bounds checks

            uint tupleNumber = value;

            // These must be explicity typed as ReadOnlySpan<byte>
            // They then become a non-allocating mappings to the data section of the assembly.
            // This uses C# compiler's ability to refer to static data directly. For more information see https://vcsjones.dev/2019/02/01/csharp-readonly-span-bytes-static 
            ReadOnlySpan<byte> shuffleMaskData = new byte[16]
            {
                    0xF, 0xF, 3, 0xF,
                    0xF, 0xF, 2, 0xF,
                    0xF, 0xF, 1, 0xF,
                    0xF, 0xF, 0, 0xF
            };

            ReadOnlySpan<byte> asciiUpperCaseData = new byte[16]
            {
                    (byte)'0', (byte)'1', (byte)'2', (byte)'3',
                    (byte)'4', (byte)'5', (byte)'6', (byte)'7',
                    (byte)'8', (byte)'9', (byte)'A', (byte)'B',
                    (byte)'C', (byte)'D', (byte)'E', (byte)'F'
            };

            // Load from data section memory into Vector128 registers
            var shuffleMask = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(shuffleMaskData));
            var asciiUpperCase = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(asciiUpperCaseData));

            var lowNibbles = Ssse3.Shuffle(Vector128.CreateScalarUnsafe(tupleNumber).AsByte(), shuffleMask);
            var highNibbles = Sse2.ShiftRightLogical(Sse2.ShiftRightLogical128BitLane(lowNibbles, 2).AsInt32(), 4).AsByte();
            var indices = Sse2.And(Sse2.Or(lowNibbles, highNibbles), Vector128.Create((byte)0xF));
            // Lookup the hex values at the positions of the indices
            var hex = Ssse3.Shuffle(asciiUpperCase, indices);
            // The high bytes (0x00) of the chars have also been converted to ascii hex '0', so clear them out.
            hex = Sse2.And(hex, Vector128.Create((ushort)0xFF).AsByte());

            // This generates much more efficient asm than fixing the buffer and using
            // Sse2.Store((byte*)(p + i), chars.AsByte());
            Unsafe.WriteUnaligned(
                ref Unsafe.As<char, byte>(
                    ref MemoryMarshal.GetArrayDataReference(buffer)),
                hex);
        }

        [Arguments((uint)0xdeadbeef)]
        [Benchmark]
        public void UseStaticMap_Safe(uint value)
        {
            char[] buffer = _buffer;
            _ = buffer[7]; // elide future bounds checks

            int number = (int)value;

            // This must be explicity typed as ReadOnlySpan<byte>
            // This then becomes a non-allocating mapping to the data section of the assembly.
            // If it is a var, Span<byte> or byte[], it allocates the byte array per call.
            ReadOnlySpan<byte> hexEncodeMap = new byte[] { (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'9', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F' };
            // Note: this only works with byte due to endian ambiguity for other types,
            // hence the later (char) casts

            buffer[7] = (char)hexEncodeMap[number & 0xF];
            buffer[6] = (char)hexEncodeMap[(number >> 4) & 0xF];
            buffer[5] = (char)hexEncodeMap[(number >> 8) & 0xF];
            buffer[4] = (char)hexEncodeMap[(number >> 12) & 0xF];
            buffer[3] = (char)hexEncodeMap[(number >> 16) & 0xF];
            buffer[2] = (char)hexEncodeMap[(number >> 20) & 0xF];
            buffer[1] = (char)hexEncodeMap[(number >> 24) & 0xF];
            buffer[0] = (char)hexEncodeMap[(number >> 28) & 0xF];
        }

        [Arguments((uint)0xdeadbeef)]
        [Benchmark]
        public void UseBitTwiddle_Safe(uint value)
        {
            char[] buffer = _buffer;
            _ = buffer.Length; // elide null checks
            Span<char> sp = buffer;

            //HexConverter.ToCharsBuffer(value, sp, 6);
            //HexConverter.ToCharsBuffer(value >>= 8, sp, 4);
            //HexConverter.ToCharsBuffer(value >>= 8, sp, 2);
            //HexConverter.ToCharsBuffer(value >>= 8, sp, 0);

            HexConverter.ToCharsBuffer_8(value, sp, 0);
        }

        [Arguments((uint)0xdeadbeef)]
        [Benchmark]
        public void UseBitTwiddle_Safe_B(uint value)
        {
            char[] buffer = _buffer;
            _ = buffer.Length; // elide null checks
            Span<char> sp = buffer;

            //HexConverter.ToCharsBuffer(value, sp, 6);
            //HexConverter.ToCharsBuffer(value >>= 8, sp, 4);
            //HexConverter.ToCharsBuffer(value >>= 8, sp, 2);
            //HexConverter.ToCharsBuffer(value >>= 8, sp, 0);

            HexConverter.ToCharsBuffer_8B(value, sp, 0);
        }

        internal static class HexConverter
        {
            public enum Casing : uint
            {
                // Output [ '0' .. '9' ] and [ 'A' .. 'F' ].
                Upper = 0,

                // Output [ '0' .. '9' ] and [ 'a' .. 'f' ].
                // This works because values in the range [ 0x30 .. 0x39 ] ([ '0' .. '9' ])
                // already have the 0x20 bit set, so ORing them with 0x20 is a no-op,
                // while outputs in the range [ 0x41 .. 0x46 ] ([ 'A' .. 'F' ])
                // don't have the 0x20 bit set, so ORing them maps to
                // [ 0x61 .. 0x66 ] ([ 'a' .. 'f' ]), which is what we want.
                Lower = 0x2020U,
            }

            // We want to pack the incoming byte into a single integer [ 0000 HHHH 0000 LLLL ],
            // where HHHH and LLLL are the high and low nibbles of the incoming byte. Then
            // subtract this integer from a constant minuend as shown below.
            //
            //   [ 1000 1001 1000 1001 ]
            // - [ 0000 HHHH 0000 LLLL ]
            // =========================
            //   [ *YYY **** *ZZZ **** ]
            //
            // The end result of this is that YYY is 0b000 if HHHH <= 9, and YYY is 0b111 if HHHH >= 10.
            // Similarly, ZZZ is 0b000 if LLLL <= 9, and ZZZ is 0b111 if LLLL >= 10.
            // (We don't care about the value of asterisked bits.)
            //
            // To turn a nibble in the range [ 0 .. 9 ] into hex, we calculate hex := nibble + 48 (ascii '0').
            // To turn a nibble in the range [ 10 .. 15 ] into hex, we calculate hex := nibble - 10 + 65 (ascii 'A').
            //                                                                => hex := nibble + 55.
            // The difference in the starting ASCII offset is (55 - 48) = 7, depending on whether the nibble is <= 9 or >= 10.
            // Since 7 is 0b111, this conveniently matches the YYY or ZZZ value computed during the earlier subtraction.

            // The commented out code below is code that directly implements the logic described above.

            // uint packedOriginalValues = (((uint)value & 0xF0U) << 4) + ((uint)value & 0x0FU);
            // uint difference = 0x8989U - packedOriginalValues;
            // uint add7Mask = (difference & 0x7070U) >> 4; // line YYY and ZZZ back up with the packed values
            // uint packedResult = packedOriginalValues + add7Mask + 0x3030U /* ascii '0' */;

            // The code below is equivalent to the commented out code above but has been tweaked
            // to allow codegen to make some extra optimizations.

            // The low byte of the packed result contains the hex representation of the incoming byte's low nibble.
            // The adjacent byte of the packed result contains the hex representation of the incoming byte's high nibble.

            // Finally, write to the output buffer starting with the *highest* index so that codegen can
            // elide all but the first bounds check. (This only works if 'startingIndex' is a compile-time constant.)

            // The JIT can elide bounds checks if 'startingIndex' is constant and if the caller is
            // writing to a span of known length (or the caller has already checked the bounds of the
            // furthest access).
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ToBytesBuffer(byte value, Span<byte> buffer, int startingIndex = 0, Casing casing = Casing.Upper)
            {
                uint difference = (((uint)value & 0xF0U) << 4) + ((uint)value & 0x0FU) - 0x8989U;
                uint packedResult = ((((uint)(-(int)difference) & 0x7070U) >> 4) + difference + 0xB9B9U) | (uint)casing;

                buffer[startingIndex + 1] = (byte)packedResult;
                buffer[startingIndex] = (byte)(packedResult >> 8);
            }

#if ALLOW_PARTIALLY_TRUSTED_CALLERS
        [System.Security.SecuritySafeCriticalAttribute]
#endif
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ToCharsBuffer(byte value, Span<char> buffer, int startingIndex = 0, Casing casing = Casing.Upper)
            {
                uint difference = (((uint)value & 0xF0U) << 4) + ((uint)value & 0x0FU) - 0x8989U;
                uint packedResult = ((((uint)(-(int)difference) & 0x7070U) >> 4) + difference + 0xB9B9U) | (uint)casing;

                buffer[startingIndex + 1] = (char)(packedResult & 0xFF);
                buffer[startingIndex] = (char)(packedResult >> 8);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ToCharsBuffer(uint value, Span<char> buffer, int startingIndex = 0, Casing casing = Casing.Upper)
            {
                uint difference = (((uint)value & 0xF0U) << 4) + ((uint)value & 0x0FU) - 0x8989U;
                uint packedResult = ((((uint)(-(int)difference) & 0x7070U) >> 4) + difference + 0xB9B9U) | (uint)casing;

                buffer[startingIndex + 1] = (char)(packedResult & 0xFF);
                buffer[startingIndex] = (char)(packedResult >> 8);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ToCharsBuffer_8(uint value, Span<char> buffer, int startingIndex = 0, Casing casing = Casing.Upper)
            {
                ulong difference =
                    ((ulong)(value & 0xFu) << 0)
                    + ((ulong)(value & 0xF0u) << 4)
                    + ((ulong)(value & 0xF00u) << 8)
                    + ((ulong)(value & 0xF000u) << 12)
                    + ((ulong)(value & 0xF0000u) << 16)
                    + ((ulong)(value & 0xF00000u) << 24)
                    + ((ulong)(value & 0xF000000u) << 32)
                    + ((ulong)(value & 0xF0000000u) << 36)
                    - 0x8989898989898989UL;

                // uint difference = (((uint)value & 0xF0U) << 4) + ((uint)value & 0x0FU) - 0x8989U;
                ulong packedResult = ((((ulong)(-(long)difference) & 0x7070707070707070UL) >> 4) + difference + 0xB9B9B9B9B9B9B9B9UL) | (uint)casing;

                buffer[startingIndex + 7] = (char)(byte)(packedResult);
                buffer[startingIndex + 6] = (char)(byte)(packedResult >>= 8);
                buffer[startingIndex + 5] = (char)(byte)(packedResult >>= 8);
                buffer[startingIndex + 4] = (char)(byte)(packedResult >>= 8);
                buffer[startingIndex + 3] = (char)(byte)(packedResult >>= 8);
                buffer[startingIndex + 2] = (char)(byte)(packedResult >>= 8);
                buffer[startingIndex + 1] = (char)(byte)(packedResult >>= 8);
                buffer[startingIndex + 0] = (char)(byte)(packedResult >>= 8);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ToCharsBuffer_8B(uint value, Span<char> buffer, int startingIndex = 0, Casing casing = Casing.Upper)
            {
                ulong difference = Bmi2.X64.ParallelBitDeposit(value, 0x0F0F0F0F_0F0F0F0Ful)
                    - 0x8989898989898989UL;

                // uint difference = (((uint)value & 0xF0U) << 4) + ((uint)value & 0x0FU) - 0x8989U;
                ulong packedResult = ((((ulong)(-(long)difference) & 0x7070707070707070UL) >> 4) + difference + 0xB9B9B9B9B9B9B9B9UL) | (uint)casing;

                buffer[startingIndex + 7] = (char)(byte)(packedResult);
                buffer[startingIndex + 6] = (char)(byte)(packedResult >>= 8);
                buffer[startingIndex + 5] = (char)(byte)(packedResult >>= 8);
                buffer[startingIndex + 4] = (char)(byte)(packedResult >>= 8);
                buffer[startingIndex + 3] = (char)(byte)(packedResult >>= 8);
                buffer[startingIndex + 2] = (char)(byte)(packedResult >>= 8);
                buffer[startingIndex + 1] = (char)(byte)(packedResult >>= 8);
                buffer[startingIndex + 0] = (char)(byte)(packedResult >>= 8);
            }

#if ALLOW_PARTIALLY_TRUSTED_CALLERS
        [System.Security.SecuritySafeCriticalAttribute]
#endif
            public static unsafe string ToString(ReadOnlySpan<byte> bytes, Casing casing = Casing.Upper)
            {
#if NET45 || NET46 || NET461 || NET462 || NET47 || NET471 || NET472 || NETSTANDARD1_0 || NETSTANDARD1_3 || NETSTANDARD2_0
            Span<char> result = stackalloc char[0];
            if (bytes.Length > 16)
            {
                var array = new char[bytes.Length * 2];
                result = array.AsSpan();
            }
            else
            {
                result = stackalloc char[bytes.Length * 2];
            }
 
            int pos = 0;
            foreach (byte b in bytes)
            {
                ToCharsBuffer(b, result, pos, casing);
                pos += 2;
            }
            return result.ToString();
#else
                fixed (byte* bytesPtr = bytes)
                {
                    return string.Create(bytes.Length * 2, (Ptr: (IntPtr)bytesPtr, bytes.Length, casing), (chars, args) =>
                    {
                        var ros = new ReadOnlySpan<byte>((byte*)args.Ptr, args.Length);
                        for (int pos = 0; pos < ros.Length; ++pos)
                        {
                            ToCharsBuffer(ros[pos], chars, pos * 2, args.casing);
                        }
                    });
                }
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static char ToCharUpper(int value)
            {
                value &= 0xF;
                value += '0';

                if (value > '9')
                {
                    value += ('A' - ('9' + 1));
                }

                return (char)value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static char ToCharLower(int value)
            {
                value &= 0xF;
                value += '0';

                if (value > '9')
                {
                    value += ('a' - ('9' + 1));
                }

                return (char)value;
            }
        }
    }
}

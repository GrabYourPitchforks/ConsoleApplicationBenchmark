using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace ConsoleAppBenchmark
{
    // [DisassemblyDiagnoser]
    public class Utf8Scenarios
    {
        private const string SampleTextsFolder = @"C:\Users\levib\source\repos\fast-utf8\FastUtf8Tester\SampleTexts\";

        private byte[] _utf8Data;

        [Params("11.txt", "11-0.txt", "25249-0.txt", "30774-0.txt", "39251-0.txt")]
        // [Params("25249-0.txt")]
        public string Corpus;

        [GlobalSetup]
        public void Setup()
        {
            _utf8Data = File.ReadAllBytes(SampleTextsFolder + Corpus);
        }

        //[Benchmark(Baseline = true)]
        //public int IterateRunesValidating()
        //{
        //    int i = default;

        //    ReadOnlySpan<byte> span = _utf8Data;
        //    while (!span.IsEmpty)
        //    {
        //        OperationStatus status = Rune.DecodeFromUtf8(span, out Rune result, out int bytesConsumed);
        //        if (status != OperationStatus.Done) { ThrowException(); }
        //        i = result.Value;
        //        span = span.Slice(bytesConsumed);
        //    }

        //    return i;
        //}

        //[Benchmark(Baseline = false)]
        //public int IterateRunesAssumeValid()
        //{
        //    int i = default;

        //    ReadOnlySpan<byte> span = _utf8Data;
        //    while (!span.IsEmpty)
        //    {
        //        i = GetNextRuneUtf8(span, out int bytesConsumed);
        //        span = span.Slice(bytesConsumed);
        //    }

        //    return i;
        //}

        [Benchmark(Baseline = true)]
        public int EncodingUtf8GetCharCount()
        {
            return Encoding.UTF8.GetCharCount(_utf8Data);
        }

        [Benchmark(Baseline = false)]
        public int EncodingUtf8GetCharCountAssumeValid()
        {
            return GetUtf16CharCountFromKnownWellFormedUtf8(_utf8Data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetNextRuneUtf8(ReadOnlySpan<byte> input, out int byteCount)
        {
            if (input.IsEmpty) { ThrowException(); }
            return GetNextRuneUtf8(ref MemoryMarshal.GetReference(input), out byteCount);
        }

        private static int GetNextRuneUtf8(ref byte rb, out int byteCount)
        {
            uint scalar = rb;
            if (scalar <= 0x7F)
            {
                byteCount = 1;
                return (int)scalar;
            }

            if ((byte)scalar < 0xE0)
            {
                scalar <<= 6;
                scalar += Unsafe.Add(ref rb, 1);
                scalar -= (0xC0 << 6) + 0x80;
                byteCount = 2;
                return (int)scalar;
            }

            if ((byte)scalar < 0xF0)
            {
                scalar <<= 12;
                scalar += (uint)Unsafe.Add(ref rb, 1) << 6;
                scalar += Unsafe.Add(ref rb, 2);
                scalar -= (0xE0 << 12) + (0x80 << 6) + 0x80;
                byteCount = 3;
                return (int)scalar;
            }

            scalar <<= 18;
            scalar += (uint)Unsafe.Add(ref rb, 1) << 12;
            scalar += (uint)Unsafe.Add(ref rb, 2) << 6;
            scalar += Unsafe.Add(ref rb, 3);
            scalar -= (0xF0 << 18) + (0x80 << 12) + (0x80 << 6) + 0x80;
            byteCount = 4;
            return (int)scalar;
        }

        private static void ThrowException()
        {
            throw new InvalidOperationException();
        }

        public static unsafe int GetUtf16CharCountFromKnownWellFormedUtf8(ReadOnlySpan<byte> utf8Data)
        {
            // Remember: the number of resulting UTF-16 chars will never be greater than the number
            // of UTF-8 bytes given well-formed input, so we can get away with casting the final
            // result to an 'int'.

            fixed (byte* pPinnedUtf8Data = &MemoryMarshal.GetReference(utf8Data))
            {
                if (Sse2.IsSupported && Popcnt.IsSupported)
                {
                    // Optimizations via SSE2 & POPCNT are available - use them.

                    Debug.Assert(BitConverter.IsLittleEndian, "SSE2 only supported on little-endian platforms.");
                    Debug.Assert(sizeof(nint) == IntPtr.Size, "nint defined incorrectly.");
                    Debug.Assert(sizeof(nuint) == IntPtr.Size, "nuint defined incorrectly.");

                    byte* pBuffer = pPinnedUtf8Data;
                    nuint bufferLength = (uint)utf8Data.Length;

                    // Optimization: Can we stay in the all-ASCII code paths?

                    nuint utf16CharCount = GetIndexOfFirstNonAsciiByte_Sse2(pBuffer, bufferLength);

                    if (utf16CharCount != bufferLength)
                    {
                        // Found at least one non-ASCII byte, so fall down the slower (but still vectorized) code paths.
                        // Given well-formed UTF-8 input, we can compute the number of resulting UTF-16 code units
                        // using the following formula:
                        //
                        // utf16CharCount = utf8ByteCount - numUtf8ContinuationBytes + numUtf8FourByteHeaders

                        utf16CharCount = bufferLength;

                        Vector128<sbyte> vecAllC0 = Vector128.Create(unchecked((sbyte)0xC0));
                        Vector128<sbyte> vecAll80 = Vector128.Create(unchecked((sbyte)0x80));
                        Vector128<sbyte> vecAll6F = Vector128.Create(unchecked((sbyte)0x6F));

                        {
                            // Perform an aligned read of the first part of the buffer.
                            // We'll mask out any data at the start of the buffer we don't care about.
                            //
                            // For example, if (pBuffer MOD 16) = 2:
                            // [ AA BB CC DD ... ] <-- original vector
                            // [ 00 00 CC DD ... ] <-- after PANDN operation

                            nint offset = -((nint)pBuffer & (sizeof(Vector128<sbyte>) - 1));
                            Vector128<sbyte> shouldBeMaskedOut = Sse2.CompareGreaterThan(Vector128.Create((byte)((int)offset + sizeof(Vector128<sbyte>) - 1)).AsSByte(), VectorOfElementIndices);
                            Vector128<sbyte> thisVector = Sse2.AndNot(shouldBeMaskedOut, Unsafe.Read<Vector128<sbyte>>(pBuffer + offset));

                            // If there's any data at the end of the buffer we don't care about, mask it out now.
                            // If this happens the 'bufferLength' value will be a lie, but it'll cause all of the
                            // branches later in the method to be skipped, so it's not a huge problem.

                            if (bufferLength < (nuint)offset + (uint)sizeof(Vector128<sbyte>))
                            {
                                Vector128<sbyte> shouldBeAllowed = Sse2.CompareLessThan(VectorOfElementIndices, Vector128.Create((byte)((int)bufferLength - (int)offset)).AsSByte());
                                thisVector = Sse2.And(shouldBeAllowed, thisVector);
                                bufferLength = (nuint)offset + (uint)sizeof(Vector128<sbyte>);
                            }

                            uint maskOfContinuationBytes = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(vecAllC0, thisVector));
                            uint countOfContinuationBytes = Popcnt.PopCount(maskOfContinuationBytes);
                            utf16CharCount -= countOfContinuationBytes;

                            uint maskOfFourByteHeaders = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(thisVector, vecAll80), vecAll6F));
                            uint countOfFourByteHeaders = Popcnt.PopCount(maskOfFourByteHeaders);
                            utf16CharCount += countOfFourByteHeaders;

                            bufferLength -= (nuint)offset;
                            bufferLength -= (uint)sizeof(Vector128<sbyte>);

                            pBuffer += offset;
                            pBuffer += (uint)sizeof(Vector128<sbyte>);
                        }

                        // At this point, pBuffer is guaranteed aligned.

                        Debug.Assert((nuint)pBuffer % (uint)sizeof(Vector128<sbyte>) == 0, "pBuffer should have been aligned.");

                        while (bufferLength >= (uint)sizeof(Vector128<sbyte>))
                        {
                            Vector128<sbyte> thisVector = Sse2.LoadAlignedVector128((sbyte*)pBuffer);

                            uint maskOfContinuationBytes = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(vecAllC0, thisVector));
                            uint countOfContinuationBytes = Popcnt.PopCount(maskOfContinuationBytes);
                            utf16CharCount -= countOfContinuationBytes;

                            uint maskOfFourByteHeaders = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(thisVector, vecAll80), vecAll6F));
                            uint countOfFourByteHeaders = Popcnt.PopCount(maskOfFourByteHeaders);
                            utf16CharCount += countOfFourByteHeaders;

                            pBuffer += sizeof(Vector128<sbyte>);
                            bufferLength -= (uint)sizeof(Vector128<sbyte>);
                        }

                        if ((uint)bufferLength > 0)
                        {
                            // There's still more data to be read.
                            // We need to mask out elements of the vector we don't care about.
                            // These elements will occur at the end of the vector.
                            //
                            // For example, if 14 bytes remain in the input stream:
                            // [ ... CC DD EE FF ] <-- original vector
                            // [ ... CC DD 00 00 ] <-- after PANDN operation

                            Vector128<sbyte> shouldBeMaskedOut = Sse2.CompareGreaterThan(VectorOfElementIndices, Vector128.Create((byte)((int)bufferLength - 1)).AsSByte());
                            Vector128<sbyte> thisVector = Sse2.AndNot(shouldBeMaskedOut, *(Vector128<sbyte>*)pBuffer);

                            uint maskOfContinuationBytes = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(vecAllC0, thisVector));
                            uint countOfContinuationBytes = Popcnt.PopCount(maskOfContinuationBytes);
                            utf16CharCount -= countOfContinuationBytes;

                            uint maskOfFourByteHeaders = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(thisVector, vecAll80), vecAll6F));
                            uint countOfFourByteHeaders = Popcnt.PopCount(maskOfFourByteHeaders);
                            utf16CharCount += countOfFourByteHeaders;
                        }
                    }

                    return (int)utf16CharCount;
                }
                else
                {
                    // Cannot use SSE2 & POPCNT. Fall back to slower code paths.

                    throw new NotImplementedException();
                }
            }
        }

        private static unsafe nuint GetIndexOfFirstNonAsciiByte_Sse2(byte* pBuffer, nuint bufferLength)
        {
            // JIT turns the below into constants

            uint SizeOfVector128 = (uint)Unsafe.SizeOf<Vector128<byte>>();
            nuint MaskOfAllBitsInVector128 = (nuint)(SizeOfVector128 - 1);

            Debug.Assert(Sse2.IsSupported, "Should've been checked by caller.");
            Debug.Assert(BitConverter.IsLittleEndian, "SSE2 assumes little-endian.");

            uint currentMask, secondMask;
            byte* pOriginalBuffer = pBuffer;

            // This method is written such that control generally flows top-to-bottom, avoiding
            // jumps as much as possible in the optimistic case of a large enough buffer and
            // "all ASCII". If we see non-ASCII data, we jump out of the hot paths to targets
            // after all the main logic.

            if (bufferLength < SizeOfVector128)
            {
                goto InputBufferLessThanOneVectorInLength; // can't vectorize; drain primitives instead
            }

            // Read the first vector unaligned.

            currentMask = (uint)Sse2.MoveMask(Sse2.LoadVector128(pBuffer)); // unaligned load

            if (currentMask != 0)
            {
                goto FoundNonAsciiDataInCurrentMask;
            }

            // If we have less than 32 bytes to process, just go straight to the final unaligned
            // read. There's no need to mess with the loop logic in the middle of this method.

            if (bufferLength < 2 * SizeOfVector128)
            {
                goto IncrementCurrentOffsetBeforeFinalUnalignedVectorRead;
            }

            // Now adjust the read pointer so that future reads are aligned.

            pBuffer = (byte*)(((nuint)pBuffer + SizeOfVector128) & ~(nuint)MaskOfAllBitsInVector128);

#if DEBUG
            long numBytesRead = pBuffer - pOriginalBuffer;
            Debug.Assert(0 < numBytesRead && numBytesRead <= SizeOfVector128, "We should've made forward progress of at least one byte.");
            Debug.Assert((nuint)numBytesRead <= bufferLength, "We shouldn't have read past the end of the input buffer.");
#endif

            // Adjust the remaining length to account for what we just read.

            bufferLength += (nuint)pOriginalBuffer;
            bufferLength -= (nuint)pBuffer;

            // The buffer is now properly aligned.
            // Read 2 vectors at a time if possible.

            if (bufferLength >= 2 * SizeOfVector128)
            {
                byte* pFinalVectorReadPos = (byte*)((nuint)pBuffer + bufferLength - 2 * SizeOfVector128);

                // After this point, we no longer need to update the bufferLength value.

                do
                {
                    Vector128<byte> firstVector = Sse2.LoadAlignedVector128(pBuffer);
                    Vector128<byte> secondVector = Sse2.LoadAlignedVector128(pBuffer + SizeOfVector128);

                    currentMask = (uint)Sse2.MoveMask(firstVector);
                    secondMask = (uint)Sse2.MoveMask(secondVector);

                    if ((currentMask | secondMask) != 0)
                    {
                        goto FoundNonAsciiDataInInnerLoop;
                    }

                    pBuffer += 2 * SizeOfVector128;
                } while (pBuffer <= pFinalVectorReadPos);
            }

            // We have somewhere between 0 and (2 * vector length) - 1 bytes remaining to read from.
            // Since the above loop doesn't update bufferLength, we can't rely on its absolute value.
            // But we _can_ rely on it to tell us how much remaining data must be drained by looking
            // at what bits of it are set. This works because had we updated it within the loop above,
            // we would've been adding 2 * SizeOfVector128 on each iteration, but we only care about
            // bits which are less significant than those that the addition would've acted on.

            // If there is fewer than one vector length remaining, skip the next aligned read.

            if ((bufferLength & SizeOfVector128) == 0)
            {
                goto DoFinalUnalignedVectorRead;
            }

            // At least one full vector's worth of data remains, so we can safely read it.
            // Remember, at this point pBuffer is still aligned.

            currentMask = (uint)Sse2.MoveMask(Sse2.LoadAlignedVector128(pBuffer));
            if (currentMask != 0)
            {
                goto FoundNonAsciiDataInCurrentMask;
            }

        IncrementCurrentOffsetBeforeFinalUnalignedVectorRead:

            pBuffer += SizeOfVector128;

        DoFinalUnalignedVectorRead:

            if (((byte)bufferLength & MaskOfAllBitsInVector128) != 0)
            {
                // Perform an unaligned read of the last vector.
                // We need to adjust the pointer because we're re-reading data.

                pBuffer += (bufferLength & MaskOfAllBitsInVector128) - SizeOfVector128;

                currentMask = (uint)Sse2.MoveMask(Sse2.LoadVector128(pBuffer)); // unaligned load
                if (currentMask != 0)
                {
                    goto FoundNonAsciiDataInCurrentMask;
                }

                pBuffer += SizeOfVector128;
            }

        Finish:

            return (nuint)pBuffer - (nuint)pOriginalBuffer; // and we're done!

        FoundNonAsciiDataInInnerLoop:

            // If the current (first) mask isn't the mask that contains non-ASCII data, then it must
            // instead be the second mask. If so, skip the entire first mask and drain ASCII bytes
            // from the second mask.

            if (currentMask == 0)
            {
                pBuffer += SizeOfVector128;
                currentMask = secondMask;
            }

        FoundNonAsciiDataInCurrentMask:

            // The mask contains - from the LSB - a 0 for each ASCII byte we saw, and a 1 for each non-ASCII byte.
            // Tzcnt is the correct operation to count the number of zero bits quickly. If this instruction isn't
            // available, we'll fall back to a normal loop.

            Debug.Assert(currentMask != 0, "Shouldn't be here unless we see non-ASCII data.");
            pBuffer += (uint)BitOperations.TrailingZeroCount(currentMask);

            goto Finish;

        FoundNonAsciiDataInCurrentDWord:

            uint currentDWord;
            Debug.Assert(!AllBytesInUInt32AreAscii(currentDWord), "Shouldn't be here unless we see non-ASCII data.");
            pBuffer += CountNumberOfLeadingAsciiBytesFromUInt32WithSomeNonAsciiData(currentDWord);

            goto Finish;

        InputBufferLessThanOneVectorInLength:

            // These code paths get hit if the original input length was less than one vector in size.
            // We can't perform vectorized reads at this point, so we'll fall back to reading primitives
            // directly. Note that all of these reads are unaligned.

            Debug.Assert(bufferLength < SizeOfVector128);

            // QWORD drain

            if ((bufferLength & 8) != 0)
            {
                if (Bmi1.X64.IsSupported)
                {
                    // If we can use 64-bit tzcnt to count the number of leading ASCII bytes, prefer it.

                    ulong candidateUInt64 = Unsafe.ReadUnaligned<ulong>(pBuffer);
                    if (!AllBytesInUInt64AreAscii(candidateUInt64))
                    {
                        // Clear everything but the high bit of each byte, then tzcnt.
                        // Remember the / 8 at the end to convert bit count to byte count.

                        candidateUInt64 &= UInt64HighBitsOnlyMask;
                        pBuffer += (nuint)(Bmi1.X64.TrailingZeroCount(candidateUInt64) / 8);
                        goto Finish;
                    }
                }
                else
                {
                    // If we can't use 64-bit tzcnt, no worries. We'll just do 2x 32-bit reads instead.

                    currentDWord = Unsafe.ReadUnaligned<uint>(pBuffer);
                    uint nextDWord = Unsafe.ReadUnaligned<uint>(pBuffer + 4);

                    if (!AllBytesInUInt32AreAscii(currentDWord | nextDWord))
                    {
                        // At least one of the values wasn't all-ASCII.
                        // We need to figure out which one it was and stick it in the currentMask local.

                        if (AllBytesInUInt32AreAscii(currentDWord))
                        {
                            currentDWord = nextDWord; // this one is the culprit
                            pBuffer += 4;
                        }

                        goto FoundNonAsciiDataInCurrentDWord;
                    }
                }

                pBuffer += 8; // successfully consumed 8 ASCII bytes
            }

            // DWORD drain

            if ((bufferLength & 4) != 0)
            {
                currentDWord = Unsafe.ReadUnaligned<uint>(pBuffer);

                if (!AllBytesInUInt32AreAscii(currentDWord))
                {
                    goto FoundNonAsciiDataInCurrentDWord;
                }

                pBuffer += 4; // successfully consumed 4 ASCII bytes
            }

            // WORD drain
            // (We movzx to a DWORD for ease of manipulation.)

            if ((bufferLength & 2) != 0)
            {
                currentDWord = Unsafe.ReadUnaligned<ushort>(pBuffer);

                if (!AllBytesInUInt32AreAscii(currentDWord))
                {
                    // We only care about the 0x0080 bit of the value. If it's not set, then we
                    // increment currentOffset by 1. If it's set, we don't increment it at all.

                    pBuffer += (nuint)((nint)(sbyte)currentDWord >> 7) + 1;
                    goto Finish;
                }

                pBuffer += 2; // successfully consumed 2 ASCII bytes
            }

            // BYTE drain

            if ((bufferLength & 1) != 0)
            {
                // sbyte has non-negative value if byte is ASCII.

                if (*(sbyte*)(pBuffer) >= 0)
                {
                    pBuffer++; // successfully consumed a single byte
                }
            }

            goto Finish;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool AllBytesInUInt32AreAscii(uint value)
        {
            // If the high bit of any byte is set, that byte is non-ASCII.

            return (value & UInt32HighBitsOnlyMask) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllBytesInUInt64AreAscii(ulong value)
        {
            // If the high bit of any byte is set, that byte is non-ASCII.

            return (value & UInt64HighBitsOnlyMask) == 0;
        }

        private const uint UInt32HighBitsOnlyMask = 0x80808080u;
        private const ulong UInt64HighBitsOnlyMask = 0x80808080_80808080ul;

        /// <summary>
        /// Given a DWORD which represents a four-byte buffer read in machine endianness, and which
        /// the caller has asserted contains a non-ASCII byte *somewhere* in the data, counts the
        /// number of consecutive ASCII bytes starting from the beginning of the buffer. Returns
        /// a value 0 - 3, inclusive. (The caller is responsible for ensuring that the buffer doesn't
        /// contain all-ASCII data.)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint CountNumberOfLeadingAsciiBytesFromUInt32WithSomeNonAsciiData(uint value)
        {
            Debug.Assert(!AllBytesInUInt32AreAscii(value), "Caller shouldn't provide an all-ASCII value.");

            // Use BMI1 directly rather than going through BitOperations. We only see a perf gain here
            // if we're able to emit a real tzcnt instruction; the software fallback used by BitOperations
            // is too slow for our purposes since we can provide our own faster, specialized software fallback.

            if (Bmi1.IsSupported)
            {
                Debug.Assert(BitConverter.IsLittleEndian);
                return Bmi1.TrailingZeroCount(value & UInt32HighBitsOnlyMask) >> 3;
            }

            // Couldn't emit tzcnt, use specialized software fallback.
            // The 'allBytesUpToNowAreAscii' DWORD uses bit twiddling to hold a 1 or a 0 depending
            // on whether all processed bytes were ASCII. Then we accumulate all of the
            // results to calculate how many consecutive ASCII bytes are present.

            value = ~value;

            if (BitConverter.IsLittleEndian)
            {
                // Read first byte
                value >>= 7;
                uint allBytesUpToNowAreAscii = value & 1;
                uint numAsciiBytes = allBytesUpToNowAreAscii;

                // Read second byte
                value >>= 8;
                allBytesUpToNowAreAscii &= value;
                numAsciiBytes += allBytesUpToNowAreAscii;

                // Read third byte
                value >>= 8;
                allBytesUpToNowAreAscii &= value;
                numAsciiBytes += allBytesUpToNowAreAscii;

                return numAsciiBytes;
            }
            else
            {
                // BinaryPrimitives.ReverseEndianness is only implemented as an intrinsic on
                // little-endian platforms, so using it in this big-endian path would be too
                // expensive. Instead we'll just change how we perform the shifts.

                // Read first byte
                value = BitOperations.RotateLeft(value, 1);
                uint allBytesUpToNowAreAscii = value & 1;
                uint numAsciiBytes = allBytesUpToNowAreAscii;

                // Read second byte
                value = BitOperations.RotateLeft(value, 8);
                allBytesUpToNowAreAscii &= value;
                numAsciiBytes += allBytesUpToNowAreAscii;

                // Read third byte
                value = BitOperations.RotateLeft(value, 8);
                allBytesUpToNowAreAscii &= value;
                numAsciiBytes += allBytesUpToNowAreAscii;

                return numAsciiBytes;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<sbyte> ShiftBytesRightByOne_Avx2(Vector256<sbyte> v)
        {
            Debug.Assert(Avx2.IsSupported);

            // Assume v = [ 32 31 30 ... 19 18 17 | 16 15 14 ... 03 02 01 ],
            // and assume byteCount = 2 for the sake of the below examples.

            Vector256<sbyte> temp = Avx2.Permute2x128(v, v, 0x81);                // = [ 00 00 00 ... 00 00 00 | 32 31 30 ... 19 18 17 ]
            v = Avx2.ShiftRightLogical128BitLane(v, 1);             // = [ 00 00 32 ... 21 20 19 | 00 00 16 ... 05 04 03 ]
            temp = Avx2.ShiftLeftLogical128BitLane(temp, 15); // = [ 00 00 00 ... 00 00 00 | 18 17 00 ... 00 00 00 ]
            return Avx2.Or(v, temp);                                              // = [ 00 00 32 ... 21 20 19 | 18 17 16 ... 05 04 03 ]
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<sbyte> ShiftBytesRightByTwo_Avx2(Vector256<sbyte> v)
        {
            Debug.Assert(Avx2.IsSupported);

            // Assume v = [ 32 31 30 ... 19 18 17 | 16 15 14 ... 03 02 01 ],
            // and assume byteCount = 2 for the sake of the below examples.

            Vector256<sbyte> temp = Avx2.Permute2x128(v, v, 0x81);                // = [ 00 00 00 ... 00 00 00 | 32 31 30 ... 19 18 17 ]
            v = Avx2.ShiftRightLogical128BitLane(v, 2);             // = [ 00 00 32 ... 21 20 19 | 00 00 16 ... 05 04 03 ]
            temp = Avx2.ShiftLeftLogical128BitLane(temp, 14); // = [ 00 00 00 ... 00 00 00 | 18 17 00 ... 00 00 00 ]
            return Avx2.Or(v, temp);                                              // = [ 00 00 32 ... 21 20 19 | 18 17 16 ... 05 04 03 ]
        }

        private static readonly Vector128<sbyte> VectorOfElementIndices = Vector128.Create(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);
    }
}

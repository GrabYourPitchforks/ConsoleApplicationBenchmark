using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using BenchmarkDotNet.Attributes;

using nuint = System.UInt64;
using nint = System.Int64;
using System.Numerics;

namespace ConsoleAppBenchmark
{
    public class GetByteCountRunner
    {
        const int ITER_COUNT = 1_000;

        [Params("11", "11-0", "25249-0", "30774-0", "39251-0")]
        public string Filename;

        private string _contents;

        [GlobalSetup]
        public void Setup()
        {
            _contents = File.ReadAllText(@"C:\Users\levib\source\repos\fast-utf8\FastUtf8Tester\SampleTexts\" + Filename + ".txt");
        }

        //[Benchmark(Baseline = true)]
        //public int EncodingUtf8GetByteCount()
        //{
        //    int retVal = default;

        //    ReadOnlySpan<char> theSpan = _contents;

        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        retVal = Encoding.UTF8.GetByteCount(theSpan);
        //    }

        //    return retVal;
        //}

        [Benchmark]
        public int Vectorized()
        {
            int retVal = default;

            ReadOnlySpan<char> theSpan = _contents;

            for (int i = 0; i < ITER_COUNT; i++)
            {
                retVal = VectorizedGetByteCount(theSpan);
            }

            return retVal;
        }

        private static unsafe int VectorizedGetByteCount(ReadOnlySpan<char> contents)
        {
            fixed (char* pContents = &MemoryMarshal.GetReference(contents))
            {
                char* pFirstInvalidChar = GetPointerToFirstInvalidChar(pContents, contents.Length, out long utf8CodeUnitCountAdjustment, out _);

                long temp = (long)(pFirstInvalidChar - pContents) + utf8CodeUnitCountAdjustment;
                return checked((int)temp);
            }
        }
        
        private static unsafe char* GetPointerToFirstInvalidChar(char* pInputBuffer, int inputLength, out long utf8CodeUnitCountAdjustment, out int scalarCountAdjustment)
        {
            // First, we'll handle the common case of all-ASCII. If this is able to
            // consume the entire buffer, we'll skip the remainder of this method's logic.

            int numAsciiCharsConsumedJustNow = (int)GetIndexOfFirstNonAsciiChar_Sse2(pInputBuffer, (uint)inputLength);
            pInputBuffer += (uint)numAsciiCharsConsumedJustNow;
            if (numAsciiCharsConsumedJustNow == inputLength)
            {
                utf8CodeUnitCountAdjustment = 0;
                scalarCountAdjustment = 0;
                return pInputBuffer;
            }

            // If we got here, it means we saw some non-ASCII data, so within our
            // vectorized code paths below we'll handle all non-surrogate UTF-16
            // code points branchlessly. We'll only branch if we see surrogates.
            // 
            // We still optimistically assume the data is mostly ASCII. This means that the
            // number of UTF-8 code units and the number of scalars almost matches the number
            // of UTF-16 code units. As we go through the input and find non-ASCII
            // characters, we'll keep track of these "adjustment" fixups. To get the
            // total number of UTF-8 code units required to encode the input data, add
            // the UTF-8 code unit count adjustment to the number of UTF-16 code units
            // seen.  To get the total number of scalars present in the input data,
            // add the scalar count adjustment to the number of UTF-16 code units seen.

            long tempUtf8CodeUnitCountAdjustment = 0;
            int tempScalarCountAdjustment = 0;

            if (Sse41.IsSupported)
            {
                if (inputLength >= Vector128<ushort>.Count)
                {
                    Vector128<ushort> vector0080 = Vector128.Create((ushort)0x80);
                    Vector128<ushort> vector0800 = Sse2.ShiftLeftLogical(vector0080, 4); // = 0x0800
                    Vector128<ushort> vectorA800 = Vector128.Create((ushort)0xA800);
                    Vector128<short> vector8800 = Vector128.Create(unchecked((short)0x8800));

                    do
                    {
                        Vector128<ushort> utf16Data = Sse2.LoadVector128((ushort*)pInputBuffer);

                        uint mask = (uint)Sse2.MoveMask(
                            Sse2.Or(
                                Sse2.ShiftLeftLogical(Sse41.Min(utf16Data, vector0080), 8),
                                Sse2.ShiftRightLogical(Sse41.Min(utf16Data, vector0800), 4)).AsByte());

                        // Each odd bit of mask will be 1 only if the char was >= 0x0080,
                        // and each even bit of mask will be 1 only if the char was >= 0x0800.
                        //
                        // Example for UTF-16 input "[ 0123 ] [ 1234 ] ...":
                        //
                        //            ,-- set if char[1] is non-ASCII
                        //            |   ,-- set if char[0] is non-ASCII
                        //            v   v
                        // mask = ... 1 1 1 0
                        //              ^   ^-- set if char[0] is >= 0x800
                        //              `-- set if char[1] is >= 0x800
                        //
                        // This means we can popcnt the number of set bits, and the result is the
                        // number of *additional* UTF-8 bytes that each UTF-16 code unit requires as
                        // it expands. This results in the wrong count for UTF-16 surrogate code
                        // units (we just counted that each individual code unit expands to 3 bytes,
                        // but in reality a well-formed UTF-16 surrogate pair expands to 4 bytes).
                        // We'll handle this in just a moment.

                        tempUtf8CodeUnitCountAdjustment += (uint)BitOperations.PopCount(mask);

                        // Surrogates need to be special-cased for two reasons: (a) we need
                        // to account for the fact that we over-counted in the addition above;
                        // and (b) they require separate validation.

                        utf16Data = Sse2.Add(utf16Data, vectorA800);
                        mask = (uint)Sse2.MoveMask(Sse2.CompareLessThan(utf16Data.AsInt16(), vector8800).AsByte());

                        if (mask != 0)
                        {
                            // There's at least one UTF-16 surrogate code unit present.
                            // Since we performed a pmovmskb operation on the result of a 16-bit pcmpgtw,
                            // the resulting bits of 'mask' will occur in pairs:
                            // - 00 if the corresponding UTF-16 char was not a surrogate code unit;
                            // - 11 if the corresponding UTF-16 char was a surrogate code unit.
                            //
                            // A UTF-16 high/low surrogate code unit has the bit pattern [ 11011q## ######## ],
                            // where # is any bit; q = 0 represents a high surrogate, and q = 1 represents
                            // a low surrogate. Since we added 0xA800 in the vectorized operation above,
                            // our surrogate pairs will now have the bit pattern [ 10000q## ######## ].
                            // If we logical right-shift each word by 3, we'll end up with the bit pattern
                            // [ 00010000 q####### ], which means that we can immediately use pmovmskb to
                            // determine whether a given char was a high or a low surrogate.
                            //
                            // Therefore the resulting bits of 'mask2' will occur in pairs:
                            // - 00 if the corresponding UTF-16 char was a high surrogate code unit;
                            // - 01 if the corresponding UTF-16 char was a low surrogate code unit;
                            // - ## (garbage) if the corresponding UTF-16 char was not a surrogate code unit.

                            uint mask2 = (uint)Sse2.MoveMask(Sse2.ShiftRightLogical(utf16Data, 3).AsByte());

                            uint lowSurrogatesMask = mask2 & mask; // 01 only if was a low surrogate char, else 00
                            uint highSurrogatesMask = (mask2 ^ mask) & 0x5555u; // 01 only if was a high surrogate char, else 00

                            // Now check that each high surrogate is followed by a low surrogate and that each
                            // low surrogate follows a high surrogate. We make an exception for the case where
                            // the final char of the vector is a high surrogate, since we can't perform validation
                            // on it until the next iteration of the loop when we hope to consume the matching
                            // low surrogate.

                            highSurrogatesMask <<= 2;
                            if ((ushort)highSurrogatesMask != lowSurrogatesMask)
                            {
                                goto NonVectorizedLoop; // error: mismatched surrogate pair; break out of vectorized logic
                            }

                            if (highSurrogatesMask > ushort.MaxValue)
                            {
                                // There was a standalone high surrogate at the end of the vector.
                                // We'll adjust our counters so that we don't consider this char consumed.

                                highSurrogatesMask = (ushort)highSurrogatesMask; // don't allow stray high surrogate to be consumed by popcnt
                                pInputBuffer--;
                                inputLength++;
                            }

                            int surrogatePairsCount = BitOperations.PopCount(highSurrogatesMask);

                            // 2 UTF-16 chars become 1 Unicode scalar

                            tempScalarCountAdjustment -= surrogatePairsCount;

                            // Since each surrogate code unit was >= 0x0800, we eagerly assumed
                            // it'd be encoded as 3 UTF-8 code units. Each surrogate half is only
                            // encoded as 2 UTF-8 code units (for 4 UTF-8 code units total),
                            // so we'll adjust this now.

                            nint surrogatePairsCountNint = (nint)(nuint)(uint)surrogatePairsCount; // zero-extend to native int size
                            tempUtf8CodeUnitCountAdjustment -= surrogatePairsCountNint;
                            tempUtf8CodeUnitCountAdjustment -= surrogatePairsCountNint;
                        }

                        pInputBuffer += Vector128<ushort>.Count;
                        inputLength -= Vector128<ushort>.Count;
                    } while (inputLength >= Vector128<ushort>.Count);
                }
            }
            else if (Vector.IsHardwareAccelerated)
            {
                if (inputLength >= Vector<ushort>.Count)
                {
                    Vector<ushort> vector0080 = new Vector<ushort>(0x0080);
                    Vector<ushort> vector0400 = new Vector<ushort>(0x0400);
                    Vector<ushort> vector0800 = new Vector<ushort>(0x0800);
                    Vector<ushort> vectorD800 = new Vector<ushort>(0xD800);

                    do
                    {
                        // The 'twoOrMoreUtf8Bytes' and 'threeOrMoreUtf8Bytes' vectors will contain
                        // elements whose values are 0xFFFF (-1 as signed word) iff the corresponding
                        // UTF-16 code unit was >= 0x0080 and >= 0x0800, respectively. By summing these
                        // vectors, each element of the sum will contain one of three values:
                        //
                        // 0x0000 ( 0) = original char was 0000..007F
                        // 0xFFFF (-1) = original char was 0080..07FF
                        // 0xFFFE (-2) = original char was 0800..FFFF
                        //
                        // We'll negate them to produce a value 0..2 for each element, then sum all the
                        // elements together to produce the number of *additional* UTF-8 code units
                        // required to represent this UTF-16 data. This is similar to the popcnt step
                        // performed by the SSE41 code path. This will overcount surrogates, but we'll
                        // handle that shortly.

                        Vector<ushort> utf16Data = Unsafe.ReadUnaligned<Vector<ushort>>(pInputBuffer);
                        Vector<ushort> twoOrMoreUtf8Bytes = Vector.GreaterThanOrEqual(utf16Data, vector0080);
                        Vector<ushort> threeOrMoreUtf8Bytes = Vector.GreaterThanOrEqual(utf16Data, vector0800);
                        Vector<nuint> sumVector = (Vector<nuint>)(-Vector.Add(twoOrMoreUtf8Bytes, threeOrMoreUtf8Bytes));

                        // We'll try summing by a natural word (rather than a 16-bit word) at a time,
                        // which should halve the number of operations we must perform.

                        nuint popcnt = 0;
                        for (int i = 0; i < Vector<nuint>.Count; i++)
                        {
                            popcnt += sumVector[i];
                        }

                        uint popcnt32 = (uint)popcnt;
                        if (sizeof(nuint) == sizeof(ulong))
                        {
                            popcnt32 += (uint)(popcnt >> 32);
                        }

                        tempUtf8CodeUnitCountAdjustment += (ushort)popcnt32;
                        tempUtf8CodeUnitCountAdjustment += popcnt32 >> 16;

                        // Now check for surrogates.

                        utf16Data -= vectorD800;
                        Vector<ushort> surrogateChars = Vector.LessThan(utf16Data, vector0800);
                        if (surrogateChars != Vector<ushort>.Zero)
                        {
                            // There's at least one surrogate (high or low) UTF-16 code unit in
                            // the vector. We'll build up additional vectors: 'highSurrogateChars'
                            // and 'lowSurrogateChars', where the elements are 0xFFFF iff the original
                            // UTF-16 code unit was a high or low surrogate, respectively.

                            Vector<ushort> highSurrogateChars = Vector.LessThan(utf16Data, vector0400);
                            Vector<ushort> lowSurrogateChars = Vector.AndNot(surrogateChars, highSurrogateChars);

                            // We want to make sure that each high surrogate code unit is followed by
                            // a low surrogate code unit and each low surrogate code unit follows a
                            // high surrogate code unit. Since we don't have an equivalent of pmovmskb
                            // or palignr available to us, we'll do this as a loop. We won't look at
                            // the very last high surrogate char element since we don't yet know if
                            // the next vector read will have a low surrogate char element.

                            ushort surrogatePairsCount = 0;
                            for (int i = 0; i < Vector<ushort>.Count - 1; i++)
                            {
                                surrogatePairsCount -= highSurrogateChars[i];
                                if (highSurrogateChars[i] != lowSurrogateChars[i + 1])
                                {
                                    goto NonVectorizedLoop; // error: mismatched surrogate pair; break out of vectorized logic
                                }
                            }

                            if (highSurrogateChars[Vector<ushort>.Count - 1] != 0)
                            {
                                // There was a standalone high surrogate at the end of the vector.
                                // We'll adjust our counters so that we don't consider this char consumed.

                                pInputBuffer--;
                                inputLength++;
                                tempUtf8CodeUnitCountAdjustment -= 2;
                                tempScalarCountAdjustment--;
                            }

                            nint surrogatePairsCountNint = (nint)surrogatePairsCount; // zero-extend to native int size

                            // 2 UTF-16 chars become 1 Unicode scalar

                            tempScalarCountAdjustment -= (int)surrogatePairsCountNint;

                            // Since each surrogate code unit was >= 0x0800, we eagerly assumed
                            // it'd be encoded as 3 UTF-8 code units. Each surrogate half is only
                            // encoded as 2 UTF-8 code units (for 4 UTF-8 code units total),
                            // so we'll adjust this now.

                            tempUtf8CodeUnitCountAdjustment -= surrogatePairsCountNint;
                            tempUtf8CodeUnitCountAdjustment -= surrogatePairsCountNint;
                        }

                        pInputBuffer += Vector<ushort>.Count;
                        inputLength -= Vector<ushort>.Count;
                    } while (inputLength >= Vector<ushort>.Count);
                }
            }

        NonVectorizedLoop:

            // Vectorization isn't supported on our current platform, or the input was too small to benefit
            // from vectorization, or we saw invalid UTF-16 data in the vectorized code paths and need to
            // drain remaining valid chars before we report failure.

            for (; inputLength > 0; pInputBuffer++, inputLength--)
            {
                uint thisChar = pInputBuffer[0];
                if (thisChar <= 0x7F)
                {
                    continue;
                }

                // Bump adjustment by +1 for U+0080..U+07FF; by +2 for U+0800..U+FFFF.
                // This optimistically assumes no surrogates, which we'll handle shortly.

                tempUtf8CodeUnitCountAdjustment += (thisChar + 0x0001_F800u) >> 16;

                if (!IsSurrogateCodePoint(thisChar))
                {
                    continue;
                }

                // Found a surrogate char. Back out the adjustment we made above, then
                // try to consume the entire surrogate pair all at once. We won't bother
                // trying to interpret the surrogate pair as a scalar value; we'll only
                // validate that its bit pattern matches what's expected for a surrogate pair.

                tempUtf8CodeUnitCountAdjustment -= 2;

                if (inputLength == 1)
                {
                    goto Error; // input buffer too small to read a surrogate pair
                }

                thisChar = Unsafe.ReadUnaligned<uint>(pInputBuffer);
                if (((thisChar - (BitConverter.IsLittleEndian ? 0xDC00_D800u : 0xD800_DC00u)) & 0xFC00_FC00u) != 0)
                {
                    goto Error; // not a well-formed surrogate pair
                }

                tempScalarCountAdjustment--; // 2 UTF-16 code units -> 1 scalar
                tempUtf8CodeUnitCountAdjustment += 2; // 2 UTF-16 code units -> 4 UTF-8 code units

                pInputBuffer++; // consumed one extra char
                inputLength--;
            }

        Error:

            // Also used for normal return.

            utf8CodeUnitCountAdjustment = tempUtf8CodeUnitCountAdjustment;
            scalarCountAdjustment = tempScalarCountAdjustment;
            return pInputBuffer;
        }


        /// <summary>
        /// Returns <see langword="true"/> iff <paramref name="value"/> is a UTF-16 high surrogate code point,
        /// i.e., is in [ U+D800..U+DBFF ], inclusive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHighSurrogateCodePoint(uint value) => IsInRangeInclusive(value, 0xD800U, 0xDBFFU);

        /// <summary>
        /// Returns <see langword="true"/> iff <paramref name="value"/> is between
        /// <paramref name="lowerBound"/> and <paramref name="upperBound"/>, inclusive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRangeInclusive(uint value, uint lowerBound, uint upperBound) => ((value - lowerBound) <= (upperBound - lowerBound));

        /// <summary>
        /// Returns <see langword="true"/> iff <paramref name="value"/> is a UTF-16 low surrogate code point,
        /// i.e., is in [ U+DC00..U+DFFF ], inclusive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLowSurrogateCodePoint(uint value) => IsInRangeInclusive(value, 0xDC00U, 0xDFFFU);

        /// <summary>
        /// Returns <see langword="true"/> iff <paramref name="value"/> is a UTF-16 surrogate code point,
        /// i.e., is in [ U+D800..U+DFFF ], inclusive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSurrogateCodePoint(uint value) => IsInRangeInclusive(value, 0xD800U, 0xDFFFU);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe nuint GetIndexOfFirstNonAsciiChar_Sse2(char* pBuffer, nuint bufferLength /* in chars */)
        {
            // This method contains logic optimized for both SSE2 and SSE41. Much of the logic in this method
            // will be elided by JIT once we determine which specific ISAs we support.

            // Quick check for empty inputs.

            if (bufferLength == 0)
            {
                return 0;
            }

            // JIT turns the below into constants

            uint SizeOfVector128InBytes = (uint)Unsafe.SizeOf<Vector128<byte>>();
            uint SizeOfVector128InChars = SizeOfVector128InBytes / sizeof(char);

            Debug.Assert(Sse2.IsSupported, "Should've been checked by caller.");
            Debug.Assert(BitConverter.IsLittleEndian, "SSE2 assumes little-endian.");

            Vector128<short> firstVector, secondVector;
            uint currentMask;
            char* pOriginalBuffer = pBuffer;

            if (bufferLength < SizeOfVector128InChars)
            {
                goto InputBufferLessThanOneVectorInLength; // can't vectorize; drain primitives instead
            }

            // This method is written such that control generally flows top-to-bottom, avoiding
            // jumps as much as possible in the optimistic case of "all ASCII". If we see non-ASCII
            // data, we jump out of the hot paths to targets at the end of the method.

            Vector128<short> asciiMaskForPTEST = Vector128.Create(unchecked((short)0xFF80)); // used for PTEST on supported hardware
            Vector128<ushort> asciiMaskForPMINUW = Vector128.Create((ushort)0x0080); // used for PMINUW on supported hardware
            Vector128<short> asciiMaskForPXOR = Vector128.Create(unchecked((short)0x8000)); // used for PXOR
            Vector128<short> asciiMaskForPCMPGTW = Vector128.Create(unchecked((short)0x807F)); // used for PCMPGTW

            Debug.Assert(bufferLength <= nuint.MaxValue / sizeof(char));

            // Read the first vector unaligned.

            firstVector = Sse2.LoadVector128((short*)pBuffer); // unaligned load

            if (Sse41.IsSupported)
            {
                // The SSE41-optimized code path works by forcing the 0x0080 bit in each WORD of the vector to be
                // set iff the WORD element has value >= 0x0080 (non-ASCII). Then we'll treat it as a BYTE vector
                // in order to extract the mask.
                currentMask = (uint)Sse2.MoveMask(Sse41.Min(firstVector.AsUInt16(), asciiMaskForPMINUW).AsByte());
            }
            else
            {
                // The SSE2-optimized code path works by forcing each WORD of the vector to be 0xFFFF iff the WORD
                // element has value >= 0x0080 (non-ASCII). Then we'll treat it as a BYTE vector in order to extract
                // the mask.
                currentMask = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(firstVector, asciiMaskForPXOR), asciiMaskForPCMPGTW).AsByte());
            }

            if (currentMask != 0)
            {
                goto FoundNonAsciiDataInCurrentMask;
            }

            // If we have less than 32 bytes to process, just go straight to the final unaligned
            // read. There's no need to mess with the loop logic in the middle of this method.

            // Adjust the remaining length to account for what we just read.
            // For the remainder of this code path, bufferLength will be in bytes, not chars.

            bufferLength <<= 1; // chars to bytes

            if (bufferLength < 2 * SizeOfVector128InBytes)
            {
                goto IncrementCurrentOffsetBeforeFinalUnalignedVectorRead;
            }

            // Now adjust the read pointer so that future reads are aligned.

            pBuffer = (char*)(((nuint)pBuffer + SizeOfVector128InBytes) & ~(nuint)(SizeOfVector128InBytes - 1));

#if DEBUG
            long numCharsRead = pBuffer - pOriginalBuffer;
            Debug.Assert(0 < numCharsRead && numCharsRead <= SizeOfVector128InChars, "We should've made forward progress of at least one char.");
            Debug.Assert((nuint)numCharsRead <= bufferLength, "We shouldn't have read past the end of the input buffer.");
#endif

            // Adjust remaining buffer length.

            bufferLength += (nuint)pOriginalBuffer;
            bufferLength -= (nuint)pBuffer;

            // The buffer is now properly aligned.
            // Read 2 vectors at a time if possible.

            if (bufferLength >= 2 * SizeOfVector128InBytes)
            {
                char* pFinalVectorReadPos = (char*)((nuint)pBuffer + bufferLength - 2 * SizeOfVector128InBytes);

                // After this point, we no longer need to update the bufferLength value.

                do
                {
                    firstVector = Sse2.LoadAlignedVector128((short*)pBuffer);
                    secondVector = Sse2.LoadAlignedVector128((short*)pBuffer + SizeOfVector128InChars);
                    Vector128<short> combinedVector = Sse2.Or(firstVector, secondVector);

                    if (Sse41.IsSupported)
                    {
                        // If a non-ASCII bit is set in any WORD of the combined vector, we have seen non-ASCII data.
                        // Jump to the non-ASCII handler to figure out which particular vector contained non-ASCII data.
                        if (!Sse41.TestZ(combinedVector, asciiMaskForPTEST))
                        {
                            goto FoundNonAsciiDataInFirstOrSecondVector;
                        }
                    }
                    else
                    {
                        // See comment earlier in the method for an explanation of how the below logic works.
                        if (Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(combinedVector, asciiMaskForPXOR), asciiMaskForPCMPGTW).AsByte()) != 0)
                        {
                            goto FoundNonAsciiDataInFirstOrSecondVector;
                        }
                    }

                    pBuffer += 2 * SizeOfVector128InChars;
                } while (pBuffer <= pFinalVectorReadPos);
            }

            // We have somewhere between 0 and (2 * vector length) - 1 bytes remaining to read from.
            // Since the above loop doesn't update bufferLength, we can't rely on its absolute value.
            // But we _can_ rely on it to tell us how much remaining data must be drained by looking
            // at what bits of it are set. This works because had we updated it within the loop above,
            // we would've been adding 2 * SizeOfVector128 on each iteration, but we only care about
            // bits which are less significant than those that the addition would've acted on.

            // If there is fewer than one vector length remaining, skip the next aligned read.
            // Remember, at this point bufferLength is measured in bytes, not chars.

            if ((bufferLength & SizeOfVector128InBytes) == 0)
            {
                goto DoFinalUnalignedVectorRead;
            }

            // At least one full vector's worth of data remains, so we can safely read it.
            // Remember, at this point pBuffer is still aligned.

            firstVector = Sse2.LoadAlignedVector128((short*)pBuffer);

            if (Sse41.IsSupported)
            {
                // If a non-ASCII bit is set in any WORD of the combined vector, we have seen non-ASCII data.
                // Jump to the non-ASCII handler to figure out which particular vector contained non-ASCII data.
                if (!Sse41.TestZ(firstVector, asciiMaskForPTEST))
                {
                    goto FoundNonAsciiDataInFirstVector;
                }
            }
            else
            {
                // See comment earlier in the method for an explanation of how the below logic works.
                currentMask = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(firstVector, asciiMaskForPXOR), asciiMaskForPCMPGTW).AsByte());
                if (currentMask != 0)
                {
                    goto FoundNonAsciiDataInCurrentMask;
                }
            }

        IncrementCurrentOffsetBeforeFinalUnalignedVectorRead:

            pBuffer += SizeOfVector128InChars;

        DoFinalUnalignedVectorRead:

            if (((byte)bufferLength & (SizeOfVector128InBytes - 1)) != 0)
            {
                // Perform an unaligned read of the last vector.
                // We need to adjust the pointer because we're re-reading data.

                pBuffer = (char*)((byte*)pBuffer + (bufferLength & (SizeOfVector128InBytes - 1)) - SizeOfVector128InBytes);
                firstVector = Sse2.LoadVector128((short*)pBuffer); // unaligned load

                if (Sse41.IsSupported)
                {
                    // If a non-ASCII bit is set in any WORD of the combined vector, we have seen non-ASCII data.
                    // Jump to the non-ASCII handler to figure out which particular vector contained non-ASCII data.
                    if (!Sse41.TestZ(firstVector, asciiMaskForPTEST))
                    {
                        goto FoundNonAsciiDataInFirstVector;
                    }
                }
                else
                {
                    // See comment earlier in the method for an explanation of how the below logic works.
                    currentMask = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(firstVector, asciiMaskForPXOR), asciiMaskForPCMPGTW).AsByte());
                    if (currentMask != 0)
                    {
                        goto FoundNonAsciiDataInCurrentMask;
                    }
                }

                pBuffer += SizeOfVector128InChars;
            }

        Finish:

            Debug.Assert(((nuint)pBuffer - (nuint)pOriginalBuffer) % 2 == 0, "Shouldn't have incremented any pointer by an odd byte count.");
            return ((nuint)pBuffer - (nuint)pOriginalBuffer) / sizeof(char); // and we're done! (remember to adjust for char count)

        FoundNonAsciiDataInFirstOrSecondVector:

            // We don't know if the first or the second vector contains non-ASCII data. Check the first
            // vector, and if that's all-ASCII then the second vector must be the culprit. Either way
            // we'll make sure the first vector local is the one that contains the non-ASCII data.

            // See comment earlier in the method for an explanation of how the below logic works.
            if (Sse41.IsSupported)
            {
                if (!Sse41.TestZ(firstVector, asciiMaskForPTEST))
                {
                    goto FoundNonAsciiDataInFirstVector;
                }
            }
            else
            {
                currentMask = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(firstVector, asciiMaskForPXOR), asciiMaskForPCMPGTW).AsByte());
                if (currentMask != 0)
                {
                    goto FoundNonAsciiDataInCurrentMask;
                }
            }

            // Wasn't the first vector; must be the second.

            pBuffer += SizeOfVector128InChars;
            firstVector = secondVector;

        FoundNonAsciiDataInFirstVector:

            // See comment earlier in the method for an explanation of how the below logic works.
            if (Sse41.IsSupported)
            {
                currentMask = (uint)Sse2.MoveMask(Sse41.Min(firstVector.AsUInt16(), asciiMaskForPMINUW).AsByte());
            }
            else
            {
                currentMask = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(firstVector, asciiMaskForPXOR), asciiMaskForPCMPGTW).AsByte());
            }

        FoundNonAsciiDataInCurrentMask:

            // The mask contains - from the LSB - a 0 for each ASCII byte we saw, and a 1 for each non-ASCII byte.
            // Tzcnt is the correct operation to count the number of zero bits quickly. If this instruction isn't
            // available, we'll fall back to a normal loop. (Even though the original vector used WORD elements,
            // masks work on BYTE elements, and we account for this in the final fixup.)

            Debug.Assert(currentMask != 0, "Shouldn't be here unless we see non-ASCII data.");
            pBuffer = (char*)((byte*)pBuffer + (uint)BitOperations.TrailingZeroCount(currentMask));

            goto Finish;

        FoundNonAsciiDataInCurrentDWord:

            uint currentDWord;
            Debug.Assert(!AllCharsInUInt32AreAscii(currentDWord), "Shouldn't be here unless we see non-ASCII data.");

            if (FirstCharInUInt32IsAscii(currentDWord))
            {
                pBuffer++; // skip past the ASCII char
            }

            goto Finish;

        InputBufferLessThanOneVectorInLength:

            // These code paths get hit if the original input length was less than one vector in size.
            // We can't perform vectorized reads at this point, so we'll fall back to reading primitives
            // directly. Note that all of these reads are unaligned.

            // Reminder: If this code path is hit, bufferLength is still a char count, not a byte count.
            // We skipped the code path that multiplied the count by sizeof(char).

            Debug.Assert(bufferLength < SizeOfVector128InChars);

            // QWORD drain

            if ((bufferLength & 4) != 0)
            {
                if (Bmi1.X64.IsSupported)
                {
                    // If we can use 64-bit tzcnt to count the number of leading ASCII chars, prefer it.

                    ulong candidateUInt64 = Unsafe.ReadUnaligned<ulong>(pBuffer);
                    if (!AllCharsInUInt64AreAscii(candidateUInt64))
                    {
                        // Clear the low 7 bits (the ASCII bits) of each char, then tzcnt.
                        // Remember the / 8 at the end to convert bit count to byte count,
                        // then the & ~1 at the end to treat a match in the high byte of
                        // any char the same as a match in the low byte of that same char.

                        candidateUInt64 &= 0xFF80FF80_FF80FF80ul;
                        pBuffer = (char*)((byte*)pBuffer + ((nuint)(Bmi1.X64.TrailingZeroCount(candidateUInt64) / 8) & ~(nuint)1));
                        goto Finish;
                    }
                }
                else
                {
                    // If we can't use 64-bit tzcnt, no worries. We'll just do 2x 32-bit reads instead.

                    currentDWord = Unsafe.ReadUnaligned<uint>(pBuffer);
                    uint nextDWord = Unsafe.ReadUnaligned<uint>(pBuffer + 4 / sizeof(char));

                    if (!AllCharsInUInt32AreAscii(currentDWord | nextDWord))
                    {
                        // At least one of the values wasn't all-ASCII.
                        // We need to figure out which one it was and stick it in the currentMask local.

                        if (AllCharsInUInt32AreAscii(currentDWord))
                        {
                            currentDWord = nextDWord; // this one is the culprit
                            pBuffer += 4 / sizeof(char);
                        }

                        goto FoundNonAsciiDataInCurrentDWord;
                    }
                }

                pBuffer += 4; // successfully consumed 4 ASCII chars
            }

            // DWORD drain

            if ((bufferLength & 2) != 0)
            {
                currentDWord = Unsafe.ReadUnaligned<uint>(pBuffer);

                if (!AllCharsInUInt32AreAscii(currentDWord))
                {
                    goto FoundNonAsciiDataInCurrentDWord;
                }

                pBuffer += 2; // successfully consumed 2 ASCII chars
            }

            // WORD drain
            // This is the final drain; there's no need for a BYTE drain since our elemental type is 16-bit char.

            if ((bufferLength & 1) != 0)
            {
                if (*pBuffer <= 0x007F)
                {
                    pBuffer++; // successfully consumed a single char
                }
            }

            goto Finish;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllBytesInUInt64AreAscii(ulong value)
        {
            return ((value & 0x80808080_80808080ul) == 0);
        }

        /// <summary>
        /// Returns <see langword="true"/> iff all chars in <paramref name="value"/> are ASCII.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllCharsInUInt32AreAscii(uint value)
        {
            return ((value & ~0x007F007Fu) == 0);
        }

        /// <summary>
        /// Returns <see langword="true"/> iff all chars in <paramref name="value"/> are ASCII.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllCharsInUInt64AreAscii(ulong value)
        {
            return ((value & ~0x007F007F_007F007Ful) == 0);
        }

        /// <summary>
        /// Given a DWORD which represents two packed chars in machine-endian order,
        /// <see langword="true"/> iff the first char (in machine-endian order) is ASCII.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool FirstCharInUInt32IsAscii(uint value)
        {
            return (BitConverter.IsLittleEndian && (value & 0xFF80u) == 0)
                || (!BitConverter.IsLittleEndian && (value & 0xFF800000u) == 0);
        }
    }
}

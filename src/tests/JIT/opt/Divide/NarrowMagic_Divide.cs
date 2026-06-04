// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Regression test for the "narrow magic" unsigned divide/mod optimization.
// When the dividend's value range is known to be smaller than the type's full
// width (e.g. via a cast from a smaller unsigned type, an AND mask, or an RSZ
// by a constant), the JIT should still produce results identical to the
// unoptimized reference division.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class NarrowMagic_Divide
{
    [MethodImpl(MethodImplOptions.NoInlining)] static uint  RefDiv(uint x, uint d)   => x / d;
    [MethodImpl(MethodImplOptions.NoInlining)] static uint  RefMod(uint x, uint d)   => x % d;
    [MethodImpl(MethodImplOptions.NoInlining)] static ulong RefDivL(ulong x, ulong d) => x / d;
    [MethodImpl(MethodImplOptions.NoInlining)] static ulong RefModL(ulong x, ulong d) => x % d;

    // (uint)(ushort)x / c -- the primary "narrow magic" target.
    [MethodImpl(MethodImplOptions.NoInlining)] static uint DivUShort3   (ushort x) => (uint)x /   3u;
    [MethodImpl(MethodImplOptions.NoInlining)] static uint DivUShort7   (ushort x) => (uint)x /   7u;
    [MethodImpl(MethodImplOptions.NoInlining)] static uint DivUShort10  (ushort x) => (uint)x /  10u;
    [MethodImpl(MethodImplOptions.NoInlining)] static uint DivUShort100 (ushort x) => (uint)x / 100u;
    [MethodImpl(MethodImplOptions.NoInlining)] static uint DivUShort1000(ushort x) => (uint)x /1000u;
    [MethodImpl(MethodImplOptions.NoInlining)] static uint ModUShort3   (ushort x) => (uint)x %   3u;
    [MethodImpl(MethodImplOptions.NoInlining)] static uint ModUShort10  (ushort x) => (uint)x %  10u;
    [MethodImpl(MethodImplOptions.NoInlining)] static uint ModUShort100 (ushort x) => (uint)x % 100u;

    // (int)(ushort)x / c -- signed result type but unsigned dividend range.
    [MethodImpl(MethodImplOptions.NoInlining)] static int DivIntUShort10 (int x) => (int)(ushort)x / 10;

    // (x & mask) / c -- explicit AND mask path.
    [MethodImpl(MethodImplOptions.NoInlining)] static uint DivMasked8_10 (uint x) => (x & 0xFFu)   / 10u;
    [MethodImpl(MethodImplOptions.NoInlining)] static uint DivMasked16_10(uint x) => (x & 0xFFFFu) / 10u;
    [MethodImpl(MethodImplOptions.NoInlining)] static uint ModMasked16_10(uint x) => (x & 0xFFFFu) % 10u;

    // (x >> 16) / c -- explicit RSZ shrink path.
    [MethodImpl(MethodImplOptions.NoInlining)] static uint DivShifted_10(uint x) => (x >> 16) / 10u;

    // (byte)x / c -- promotion from byte.
    [MethodImpl(MethodImplOptions.NoInlining)] static uint DivByte_10(byte x) => (uint)x / 10u;

    // (ulong)(ushort)x / c -- narrow magic on TYP_LONG should not be taken on
    // 64-bit targets today, but the unsigned-magic lowering must remain correct.
    [MethodImpl(MethodImplOptions.NoInlining)] static ulong DivULongUShort10(ulong x) => (ulong)(ushort)x / 10ul;
    [MethodImpl(MethodImplOptions.NoInlining)] static ulong ModULongUShort10(ulong x) => (ulong)(ushort)x % 10ul;

    // Full-uint baseline -- the non-narrow path must continue to work.
    [MethodImpl(MethodImplOptions.NoInlining)] static uint  DivFullUint_10(uint  x) => x / 10u;
    [MethodImpl(MethodImplOptions.NoInlining)] static ulong DivFullUlong_10(ulong x) => x / 10ul;

    [Fact]
    public static int TestEntryPoint()
    {
        // Exhaustive sweep across every ushort value -- exercises every narrow
        // dividend that the optimization is allowed to assume.
        for (int i = 0; i <= ushort.MaxValue; i++)
        {
            ushort u = (ushort)i;
            uint uu = u;
            ulong taggedU = (ulong)u | 0xDEAD_BEEF_0000_0000UL;

            if (DivUShort3   (u) != RefDiv(uu,   3u))  return 1;
            if (DivUShort7   (u) != RefDiv(uu,   7u))  return 2;
            if (DivUShort10  (u) != RefDiv(uu,  10u))  return 3;
            if (DivUShort100 (u) != RefDiv(uu, 100u))  return 4;
            if (DivUShort1000(u) != RefDiv(uu,1000u))  return 5;

            if (ModUShort3   (u) != RefMod(uu,   3u))  return 6;
            if (ModUShort10  (u) != RefMod(uu,  10u))  return 7;
            if (ModUShort100 (u) != RefMod(uu, 100u))  return 8;

            if (DivIntUShort10(i) != (int)u / 10)      return 9;

            if (DivMasked16_10((uint)i) != RefDiv(uu, 10u)) return 10;
            if (ModMasked16_10((uint)i) != RefMod(uu, 10u)) return 11;

            // RSZ-shrink path: dividend = (high16 << 16) >> 16 == high16.
            if (DivShifted_10((uint)i << 16) != RefDiv(uu, 10u)) return 12;

            if (DivULongUShort10(taggedU) != RefDivL(u, 10ul)) return 13;
            if (ModULongUShort10(taggedU) != RefModL(u, 10ul)) return 14;
        }

        // Exhaustive sweep across every byte value for the byte/AND paths.
        for (int b = 0; b <= byte.MaxValue; b++)
        {
            uint ub = (uint)b;
            if (DivByte_10((byte)b)   != RefDiv(ub, 10u)) return 20;
            if (DivMasked8_10((uint)b) != RefDiv(ub, 10u)) return 21;
        }

        // Spot checks at full-uint boundaries (non-narrow path must continue working).
        uint[]  uintEdge  = new uint[]  { 0u, 1u, 9u, 10u, 11u, 12345u, 0x7FFFFFFFu, 0x80000000u, 0xFFFFFFFEu, 0xFFFFFFFFu };
        ulong[] ulongEdge = new ulong[] { 0ul, 1ul, 9ul, 10ul, 12345ul, 0x80000000ul, ulong.MaxValue - 1, ulong.MaxValue };

        foreach (uint v in uintEdge)
        {
            if (DivFullUint_10(v) != v / 10u) return 30;
        }
        foreach (ulong v in ulongEdge)
        {
            if (DivFullUlong_10(v) != v / 10ul) return 31;
        }

        return 100;
    }
}

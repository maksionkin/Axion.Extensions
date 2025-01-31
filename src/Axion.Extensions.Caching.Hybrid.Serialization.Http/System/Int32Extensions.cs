// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Numerics;
using System.Runtime.CompilerServices;

#pragma warning disable IDE0130	// Namespace  does not match folder structure
namespace System;

static class Int32Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteHexTo(this int value, Span<byte> buffer)
    {
        var multipler = value.HexMultiplier();
        var index = 0;

        while (multipler > 0)
        {
            var d = Math.DivRem(value, multipler, out value);
            buffer[index++] = (byte)(d < 10 ? d + '0' : d + 'a' - 10);
            multipler >>= 4;
        }

        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HexMultiplier(this int value) =>
        1 << ((value.CountOfHexDigits() - 1) << 2);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountOfHexDigits(this int value)
    {
#if NETCOREAPP3_0_OR_GREATER
        return (BitOperations.Log2((uint)value) >> 2) + 1;
#else
        if (value < 16)
        {
            return 1;
        }
        else if (value < 16 * 16)
        {
            return 2;
        }
        else if (value < 16 * 16 * 16)
        {
            return 3;
        }
        else if (value < 16 * 16 * 16)
        {
            return 4;
        }
        else if (value < 16 * 16 * 16 * 16)
        {
            return 5;
        }
        else if (value < 16 * 16 * 16 * 16)
        {
            return 6;
        }
        else if (value < 16 * 16 * 16 * 16 * 16)
        {
            return 7;
        }
        else
        {
            return 8;
        }
#endif
    }
}

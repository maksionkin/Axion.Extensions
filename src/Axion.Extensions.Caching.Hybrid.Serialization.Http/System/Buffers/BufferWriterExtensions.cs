// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using Axion.Extensions.Caching.Hybrid.Serialization.Http;

#pragma warning disable IDE0130	// Namespace  does not match folder structure
namespace System.Buffers;

static partial class BufferWriterExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> SafeGetSpan<T>(this IBufferWriter<T> target, int sizeHint = 0)
    {
        var span = target.GetSpan(sizeHint);

        if (span.IsEmpty)
        {
            throw new ArgumentException("The current buffer writer can't contain the requested input data.");
        }

        return span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(this IBufferWriter<T> target, T value)
    {
        target.SafeGetSpan()[0] = value;
        target.Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLine(this IBufferWriter<byte> target) =>
        target.Write("\r\n"u8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this IBufferWriter<byte> target, HttpHeaders headers)
    {
        foreach (var header in headers)
        {
            foreach (var value in header.Value)
            {
                target.Write(header.Key);
                target.Write(": "u8);
                target.Write(value);
                target.WriteLine();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this IBufferWriter<byte> target, string value) =>
#if NET5_0_OR_GREATER
        HttpResponseMessageHybridCacheSerializer.Utf8.GetBytes(value, target);
#else
        target.Write(HttpResponseMessageHybridCacheSerializer.Utf8.GetBytes(value));
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this IBufferWriter<byte> target, Version version)
    {
        var d = version.Major;
        d = d < 0 ? 0 : (d > 9 ? 9 : d);

        Span<byte> span = stackalloc byte[4];
        span[0] = (byte)(d + '0');

        d = version.Minor;
        d = d < 0 ? 0 : (d > 9 ? 9 : d);

        if (d == 0)
        {
            span[1] = (byte)' ';
            span = span[..2];
        }
        else
        {
            span[1] = (byte)'.';
            span[2] = (byte)(d + '0');
            span[3] = (byte)' ';
        }

        target.Write(span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(this IBufferWriter<byte> target, HttpStatusCode statusCode)
    {
        var status = statusCode < 0 ? 0 : ((int)statusCode > 999 ? 999 : (int)statusCode);

        Span<byte> span = stackalloc byte[4];

        status = Math.DivRem(status, 100, out var d);
        span[0] = (byte)(status + '0');

        status = Math.DivRem(d, 10, out d);
        span[1] = (byte)(status + '0');

        span[2] = (byte)(d + '0');

        span[3] = (byte)' ';

        target.Write(span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteHex(this IBufferWriter<byte> target, int value)
    {
        if (value == 0)
        {
            target.Write((byte)'0');
        }
        else
        {
            int i;
            if (value < 16)
            {
                i = 1;
            }
            else if (value < 16 * 16)
            {
                i = 16;
            }
            else if (value < 16 * 16 * 16)
            {
                i = 16 * 16;
            }
            else
            {
                i = 16 * 16 * 16;
            }

            var span = target.SafeGetSpan();
            var written = 0;
            while (true)
            {
                var d = Math.DivRem(value, i, out value);
                span[written++] = (byte)(d + (d < 10 ? '0' : 'a' - 10));

                i >>= 4;

                if (i == 0)
                {
                    target.Advance(written);

                    break;
                }

                if (written == span.Length)
                {
                    target.Advance(written);
                    span = target.SafeGetSpan();
                    written = 0;
                }
            }
        }
    }
}

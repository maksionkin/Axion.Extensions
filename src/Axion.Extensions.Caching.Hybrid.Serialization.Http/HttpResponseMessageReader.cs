// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;

namespace Axion.Extensions.Caching.Hybrid.Serialization.Http;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
ref struct HttpResponseMessageReader(ReadOnlySequence<byte> sequence, HttpResponseMessageHybridCacheSerializer.Options options)
{
    SequenceReader<byte> reader = new(sequence);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Read(ReadOnlySpan<byte> value) =>
        reader.IsNext(value, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool ReadDigit(out int value)
    {
        if (reader.TryPeek(out var d) && d >= '0' && d <= '9')
        {
            value = d - '0';

            reader.Advance(1);

            return true;
        }

        value = default;

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Read(byte value) =>
        reader.IsNext(value, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Read([NotNullWhen(true)] out Version? value)
    {
        if (ReadDigit(out var major))
        {
            value = Read((byte)'.') && ReadDigit(out var minor)
                ? new(major, minor)
                : new(major, 0);

            return true;
        }

        value = null;

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadSpace() =>
        Read((byte)' ');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Read(out HttpStatusCode value)
    {
        if (ReadDigit(out var d0) && ReadDigit(out var d1) && ReadDigit(out var d2))
        {
            value = (HttpStatusCode)(d0 * 100 + d1 * 10 + d2);

            return true;
        }

        value = HttpStatusCode.OK;

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int ReadChar(byte b, scoped Span<char> buffer)
    {
        if (b < 0b1100_0000)
        {
            buffer[0] = (char)b;

            return 1;
        }
        else if (b < 0b1110_0000)
        {
            if (reader.TryRead(out byte b1))
            {
                buffer[0] = (char)(((b & 0b1_1111) << 6) | (b1 & 0b11_1111));

                return 1;
            }
        }
        else if (b < 0b1111_0000)
        {
            if (reader.TryRead(out var b1) && reader.TryRead(out var b2))
            {
                buffer[0] = (char)(((b & 0b1_1111) << 12) | ((b1 & 0b11_1111) << 6) | (b2 & 0b11_1111));
            }
        }
        else
        {
            if (reader.TryRead(out var b1) && reader.TryRead(out var b2) && reader.TryRead(out var b3))
            {

                var code = ((b & 0b111) << 18) | ((b1 & 0b11_1111) << 12) | ((b2 & 0b11_1111) << 6) | (b3 & 0b11_1111);

                if (code < 0x10000)
                {
                    buffer[0] = (char)code;

                    return 1;
                }
                else
                {
                    code -= 0x10000;
                    buffer[0] = (char)(0xD800 | (code >> 10));
                    buffer[1] = (char)(0xDC00 | (code & 0x3FF));

                    return 2;
                }
            }
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? ReadString(byte delimiter = (byte)'\r')
    {
        Span<char> charBuffer = stackalloc char[options.MaxCharsOnStack];
        var charIndex = 0;
        var hitDilimiter = false;

        while (charIndex + 1 < charBuffer.Length && reader.TryPeek(out var b) && !(hitDilimiter = (b == delimiter || b == '\r' || b == '\r')))
        {
            reader.Advance(1);

            var written = ReadChar(b, charBuffer[charIndex..]);
            if (written == 0)
            {
                return null;
            }

            charIndex += written;
        }

        if (hitDilimiter)
        {
            return options.StringPool.GetOrAdd(charBuffer[..charIndex]);
        }
        else
        {
            var charLength = charIndex;

            var subReader = new SequenceReader<byte>(reader.UnreadSequence);
            while (subReader.TryPeek(out var b))
            {
                if (b == delimiter || b == '\r' || b == '\n')
                {
                    break;
                }

                subReader.Advance(1);

                if ((b & 0b1100_0000) != 0b1000_0000)
                {
                    charLength++;

                    if (b >= 0b1111_0000)
                    {
                        charLength++;
                    }
                }
            }

            var arrayPool = options.CharArrayPool;
            var buffer = arrayPool.Rent(charLength);
            try
            {
                charBuffer[..charIndex].CopyTo(buffer);

                for (var i = charIndex; i < charLength;)
                {
                    if (!reader.TryRead(out var b))
                    {
                        return null;
                    }

                    var written = ReadChar(b, buffer.AsSpan(i));
                    if (written == 0)
                    {
                        return null;
                    }

                    i += written;
                }

                return options.StringPool.GetOrAdd(buffer.AsSpan(0, charLength));
            }
            finally
            {
                arrayPool.Return(buffer);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadLine() =>
        Read("\r\n"u8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadHeader([NotNullWhen(true)] out string? header, out string? value)
    {
        value = null;
        header = ReadString((byte)':');

        if (header == null)
        {
            return false;
        }

        if (Read((byte)':') && ReadSpace())
        {
            value = ReadString();

            ReadLine();

            return true;
        }
        else if (header.Length > 0)
        {
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stream ReadChuncked()
    {
        var result = new ChunckedStream(ref reader);

        ReadLine();

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stream ReadBody(long? length)
    {
        var sequence = reader.UnreadSequence;
        if (length != null)
        {
            sequence = sequence.Slice(0, length.Value);
        }

        reader.Advance(sequence.Length);

        return CommunityToolkit.HighPerformance.ReadOnlySequenceExtensions.AsStream(sequence);
    }
}

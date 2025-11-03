// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace Axion.Extensions.Caching.Hybrid.Serialization.Http;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
ref struct HttpResponseMessageWriter(
    IBufferWriter<byte> target,
#pragma warning disable CS9113
    HttpResponseMessageHybridCacheSerializer.Options options)
#pragma warning restore CS9113
{
    Span<byte> buffer;
    int offset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void EnsureBuffer()
    {
        if (buffer.Length - offset == 0)
        {
            target.Advance(buffer.Length);
            buffer = target.GetSpan();
            offset = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Flush()
    {
        if (offset > 0)
        {
            target.Advance(offset);
            offset = 0;
            buffer = default;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(scoped ReadOnlySpan<byte> value)
    {
        while (value.Length > 0)
        {
            EnsureBuffer();

            var copied = Math.Min(buffer.Length - offset, value.Length);
            value[..copied].CopyTo(buffer.Slice(offset, copied));

            value = value[copied..];

            offset += copied;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(byte value)
    {
        EnsureBuffer();

        buffer[offset++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(Version value)
    {
        var major = value.Major;
        major = major < 0 ? 0 : (major > 9 ? 9 : major);

        var minor = value.Minor;
        minor = minor < 0 ? 0 : (minor > 9 ? 9 : minor);

        if (minor == 0)
        {
            if (buffer.Length - offset >= 2)
            {
                const short c = ('0' << 8) + '/';

                BinaryPrimitives.WriteInt16LittleEndian(buffer[offset..], (short)((major << 8) + c));
                offset += 2;
            }
            else
            {
                Write((byte)'/');
                Write((byte)(major + '0'));
            }
        }
        else
        {
            if (buffer.Length - offset >= 4)
            {
                const int c = ('0' << 24) + ('.' << 16) + ('0' << 8) + '/';

                BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], (major << 8) + (minor << 24) + c);
                offset += 4;
            }
            else
            {
                Write((byte)'/');
                Write((byte)(major + '0'));
                Write((byte)'.');
                Write((byte)(minor + '0'));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(HttpStatusCode value)
    {
        var status = value < 0 ? 0 : ((int)value > 999 ? 999 : (int)value);

        var hundred = Math.DivRem(status, 100, out var d);

        var ten = Math.DivRem(d, 10, out d);

        if (buffer.Length - offset >= 4)
        {
            const int c = ('0' << 24) + ('0' << 16) + ('0' << 8) + ' ';

            BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], (d << 24) + (ten << 16) + (hundred << 8) + c);
            offset += 4;
        }
        else
        {
            Write((byte)' ');
            Write((byte)(hundred + '0'));
            Write((byte)(ten + '0'));
            Write((byte)(d + '0'));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLine() =>
        Write("\r\n"u8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteToken(ReadOnlySpan<char> value)
    {
        while (value.Length > 0)
        {
            EnsureBuffer();

            var size = Math.Min(value.Length, buffer.Length - offset);

            for (var i = 0; i < size; i++)
            {
                buffer[i + offset] = (byte)value[i];
            }

            offset += size;
            value = value[size..];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ReadOnlySpan<char> value)
    {
        for (var i = 0; i < value.Length;)
        {
            var c = value[i];
            if (c == '\r' || c == '\n')
            {
                c = ' ';
            }

            if (c < 0x80)
            {
                Write((byte)c);

                i++;
            }
            else if (c < 0x800)
            {
                Write((byte)(0b1100_0000 | ((c >> 6) & 0b01_1111)));
                Write((byte)(0b1000_0000 | (c & 0b11_1111)));

                i++;
            }
            else if (!char.IsHighSurrogate(c) && i < value.Length - 1)
            {
                Write((byte)(0b1110_0000 | ((c >> 12) & 0b1111)));
                Write((byte)(0b1000_0000 | ((c >> 6) & 0b11_1111)));
                Write((byte)(0b1000_0000 | (c & 0b11_1111)));

                i++;
            }
            else
            {
                var codePoint = char.ConvertToUtf32(c, value[i + 1]);

                Write((byte)(0b1111_0000 | ((codePoint >> 18) & 0b0111)));
                Write((byte)(0b1000_0000 | ((codePoint >> 12) & 0b11_1111)));
                Write((byte)(0b1000_0000 | ((codePoint >> 6) & 0b11_1111)));
                Write((byte)(0b1000_0000 | (codePoint & 0b11_1111)));

                i += 2;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(HttpHeaders headers)
    {
        foreach (var header in headers)
        {
            foreach (var value in header.Value)
            {
                WriteToken(header.Key.AsSpan());
                Write(": "u8);
                Write(value.AsSpan());
                WriteLine();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(Stream value)
    {
        while (true)
        {
            EnsureBuffer();

            var read = Read(value, buffer[offset..]);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
    static
#else
    readonly
#endif
    int Read(Stream stream, scoped Span<byte> buffer)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        return stream.Read(buffer);
#else
        var pool = options.ByteArrayPool;
        var array = pool.Rent(buffer.Length);
        try
        {
            var result = stream.Read(array, 0, buffer.Length);
            array.AsSpan(0, result).CopyTo(buffer);

            return result;
        }
        finally
        {
            pool.Return(array);
        }
#endif
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteChuncked(Stream value)
    {
        Span<byte> bytes = stackalloc byte[8];
        while (true)
        {
            EnsureBuffer();

            if (buffer.Length - offset < 5)
            {
                var read = Read(value, bytes);

                Write((byte)(read + '0')); // read cannot be more then 8 here
                WriteLine();

                if (read == 0)
                {
                    break;
                }

                Write(bytes[..read]);
            }
            else
            {
                var chunckSizeLength = (buffer.Length - offset - 3).CountOfHexDigits();
                var read = Read(value, buffer[(chunckSizeLength + offset + 2)..]);

                var size = read.WriteHexTo(buffer.Slice(offset, chunckSizeLength));
                buffer[size + offset] = (byte)'\r';
                buffer[size + offset + 1] = (byte)'\n';

                if (read == 0)
                {
                    offset += 3;

                    break;
                }
                else
                {
                    if (size != chunckSizeLength)
                    {
                        buffer.Slice(chunckSizeLength + offset + 2, read).CopyTo(buffer.Slice(size + offset + 2, read));
                    }

                    offset += size + 2 + read;
                }
            }

            WriteLine();
        }
    }
}

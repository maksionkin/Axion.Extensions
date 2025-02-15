// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

namespace Axion.Extensions.Caching.Hybrid.Serialization.Http;

class ChunckedStream : Stream
{
    readonly long length;
    long position;
    int currentChunkSize;
    int currentChunkLeft;
    ReadOnlySequence<byte> sequence;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChunckedStream(ref SequenceReader<byte> reader)
    {
        while (ReadHex(ref reader, out var size))
        {
            if (currentChunkSize == 0)
            {
                sequence = reader.UnreadSequence;
                currentChunkSize = currentChunkLeft = size;
            }

            length += size;

            reader.Advance(size);

            if (!reader.IsNext("\r\n"u8, true))
            {
                break;
            }

            if (size == 0)
            {
                break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool ReadHex(ref SequenceReader<byte> reader, out int value)
    {
        var read = false;
        value = 0;

        while (reader.TryPeek(out var digit))
        {
            int d;
            if (digit >= '0' && digit <= '9')
            {
                d = digit - '0';
            }
            else if (digit >= 'a' && digit <= 'f')
            {
                d = digit - 'a' + 10;
            }
            else if (digit >= 'A' && digit <= 'F')
            {
                d = digit - 'A' + 10;
            }
            else
            {
                break;
            }

            reader.Advance(1);

            value <<= 4;
            value += d;

            read = true;
        }

        return read && reader.IsNext("\r\n"u8, true);
    }

    void ReadNextChunk()
    {
        if (currentChunkSize != 0)
        {
            var reader = new SequenceReader<byte>(sequence);

            if (reader.IsNext("\r\n"u8, true) && ReadHex(ref reader, out currentChunkSize))
            {
                currentChunkLeft = currentChunkSize;

                sequence = reader.UnreadSequence;
            }
            else
            {
                currentChunkSize = currentChunkLeft = 0;
            }
        }
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => length;

    public override long Position
    {
        get => position;
        set => throw new InvalidOperationException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new InvalidOperationException();

    public override void SetLength(long value) =>
        throw new InvalidOperationException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException();

    public override int ReadByte()
    {
        Span<byte> buffer = stackalloc byte[1];

        return Read(buffer) == 0 ? -1 : buffer[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_0_OR_GREATER
    public override
#endif
    int Read(Span<byte> buffer)
    {
        if (currentChunkLeft == 0)
        {
            ReadNextChunk();
        }

        if (currentChunkSize == 0)
        {
            return 0;
        }

        var reader = new SequenceReader<byte>(sequence);

        if (buffer.Length > currentChunkLeft)
        {
            buffer = buffer[..currentChunkLeft];
        }

        reader.TryCopyTo(buffer);

        position += buffer.Length;
        currentChunkLeft -= buffer.Length;

        reader.Advance(buffer.Length);
        sequence = reader.UnreadSequence;

        return buffer.Length;
    }
}

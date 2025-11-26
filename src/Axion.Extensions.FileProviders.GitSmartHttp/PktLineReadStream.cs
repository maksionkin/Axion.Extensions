// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Axion.Extensions.FileProviders;

class PktLineReadStream(Stream stream, bool expectPack) : Stream
{
    bool ended;
    int leftBytes;
    readonly byte[] length = new byte[4];

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new InvalidOperationException();

    public override long Position { get => throw new InvalidOperationException(); set => throw new InvalidOperationException(); }

    public override void Flush() =>
        throw new InvalidOperationException();

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer, offset, count).ConfigureAwait(false).GetAwaiter().GetResult();

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    public async
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        override
#endif
        ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (ended || buffer.Length == 0)
        {
            return 0;
        }

        while (leftBytes == 0)
        {
            var read = await stream.ReadAtLeastAsync(length, 4, false, cancellationToken);
            if (read == 0)
            {
                ended = true;

                return 0;
            }

            if (expectPack && length.StartsWith("PACK"u8))
            {
                ended = true;

                return 0;
            }

            if (read != 4 || !Utf8Parser.TryParse(length, out ushort size, out var consumed, 'x') || consumed != 4 || (size != 0 && size < 4))
            {
                throw new FormatException("Unknown payload.");
            }

            leftBytes = size == 0 ? 0 : size - 4;
        }

        var toRead = Math.Min(buffer.Length, leftBytes);
        var actuallyRead = await stream.ReadAtLeastAsync(buffer[..toRead], toRead, false, cancellationToken);
        leftBytes -= actuallyRead;

        return actuallyRead;
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new InvalidOperationException();

    public override void SetLength(long value) =>
        throw new InvalidOperationException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException();
}

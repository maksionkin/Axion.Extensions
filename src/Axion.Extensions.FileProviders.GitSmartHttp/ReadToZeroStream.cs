// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Axion.Extensions.FileProviders;

class ReadToZeroStream(Stream stream) : Stream
{
    bool ended;
    readonly byte[] bytes = new byte[1];

    public override bool CanRead => true;

    public override bool CanSeek => throw new InvalidOperationException();

    public override bool CanWrite => throw new InvalidOperationException();

    public override long Length => throw new InvalidOperationException();

    public override long Position { get => throw new InvalidOperationException(); set => throw new InvalidOperationException(); }

    public override void Flush() =>
        throw new InvalidOperationException();

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer, offset, count).ConfigureAwait(false).GetAwaiter().GetResult();

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (ended)
        {
            return 0;
        }

        var read = 0;
        while (read < buffer.Length)
        {
            var b = await ReadByteAsync(cancellationToken);
            if (b == -1)
            {
                break;
            }

            buffer[read] = (byte)b;
            read++;
        }

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new InvalidOperationException();

    public override void SetLength(long value) =>
        throw new InvalidOperationException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException();

    async ValueTask<int> ReadByteAsync(CancellationToken cancellationToken = default)
    {
        if (ended)
        {
            return -1;
        }

        var read = await stream.ReadAtLeastAsync(bytes, 1, false, cancellationToken);
        if (read == 0 || bytes[0] == 0)
        {
            ended = true;
            return -1;
        }
        else
        {
            return bytes[0];
        }
    }
}

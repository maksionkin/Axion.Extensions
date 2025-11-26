// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip.Compression;

namespace Axion.Extensions.FileProviders;

class ZLibFixedStream(Stream stream, long length, HashAlgorithm? hash) : Stream
{
    readonly Inflater inflater = new();
    readonly byte[] bytes = new byte[1];

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => length;

    public override long Position { get => throw new InvalidOperationException(); set => throw new InvalidOperationException(); }

    public long TotalIn { get => inflater.TotalIn; }

    public override void Flush() =>
        throw new InvalidOperationException();

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer, offset, count).ConfigureAwait(false).GetAwaiter().GetResult();

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfLessThan(offset, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, buffer.LongLength);

        var totalRead = 0;

        while (totalRead < count)
        {
            var read = await ReadCoreAsync(buffer, offset + totalRead, count - totalRead, cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new InvalidOperationException();

    public override void SetLength(long value) =>
        throw new InvalidOperationException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException();

    async ValueTask<int> ReadCoreAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (length == 0 || buffer.Length == 0 || inflater.IsFinished)
        {
            return 0;
        }

        while (true)
        {
            var read = inflater.Inflate(buffer, offset, count);

            if (read > 0)
            {
                hash?.TransformBlock(buffer, offset, read, null, 0);

                length -= read;

                if (length == 0)
                {
                    hash?.TransformFinalBlock([], 0, 0);

                    while (!inflater.IsFinished)
                    {
                        await ReadByteAsync(cancellationToken);
                        inflater.Inflate(bytes);
                    }
                }

                return read;
            }

            await ReadByteAsync(cancellationToken);
        }
    }

    async ValueTask ReadByteAsync(CancellationToken cancellationToken)
    {
        await stream.ReadAtLeastAsync(bytes, 1, true, cancellationToken);
        inflater.SetInput(bytes);
    }
}

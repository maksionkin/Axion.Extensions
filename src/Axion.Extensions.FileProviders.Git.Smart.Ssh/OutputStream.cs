// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

namespace Axion.Extensions.FileProviders;

class OutputStream(SshCommand command, IAsyncResult asyncResult) : Stream
{
    readonly Stream stream = command.OutputStream;

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

    public
#if NET5_0_OR_GREATER
        override
#endif
        async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        await stream.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new InvalidOperationException();

    public override void SetLength(long value) =>
        throw new InvalidOperationException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {

            if (asyncResult.IsCompleted)
            {
                try
                {
                    command.EndExecute(asyncResult);
                }
                catch
                {
                }
            }
            try
            {
                command.Dispose();
            }
            catch
            {
            }
        }

        base.Dispose(disposing);
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
    public override async ValueTask DisposeAsync() =>
        Dispose(true);
#endif
}

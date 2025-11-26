// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Text;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.IO;
#pragma warning restore IDE0130 // Namespace does not match folder structure

static class StreamExtensions
{
    extension(Stream stream)
    {
        public async ValueTask SkipToEndAsync(CancellationToken cancellationToken = default) =>
            await stream.CopyToAsync(Stream.Null, 1024, cancellationToken);

        public void WritePrkLine(string text)
        {
            Span<byte> size = stackalloc byte[4];

            Utf8Formatter.TryFormat(text.Length + 4 + 1, size, out _, new('x', 4));

            stream.Write(size);
            stream.Write(Encoding.ASCII.GetBytes(text));
            stream.WriteByte((byte)'\n');
        }

        public void FlushPktLine() =>
            stream.Write("0000"u8);
    }
}

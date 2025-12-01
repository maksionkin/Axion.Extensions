// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Text;
using System.Text;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Buffers;
#pragma warning restore IDE0130 // Namespace does not match folder structure

static class BufferWriterExtensions
{
    extension(IBufferWriter<byte> writer)
    {
        public void WritePrkLine(string text)
        {
            Span<byte> size = stackalloc byte[4];

            Utf8Formatter.TryFormat(text.Length + 4 + 1, size, out _, new('x', 4));

            writer.Write(size);
            writer.Write(Encoding.ASCII.GetBytes(text));
            writer.GetSpan(1)[0] = (byte)'\n';
            writer.Advance(1);
        }

        public void FlushPktLine() =>
            writer.Write("0000"u8);
    }
}

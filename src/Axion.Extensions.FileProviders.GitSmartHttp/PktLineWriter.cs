// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers.Text;
using System.Text;

namespace Axion.Extensions.FileProviders;

class PktLineWriter(Memory<byte> memory) : IDisposable
{
    static readonly UTF8Encoding Utf8 = new(false, false);
    int length;
    bool disposedValue;

    public int Length { get => length; }

    public void WriteLine(ReadOnlySpan<byte> line)
    {
        Utf8Formatter.TryFormat(line.Length + 5, memory.Span[length..], out _, new('x', 4));
        length += 4;

        line.CopyTo(memory.Span[length..]);
        length += line.Length;

        memory.Span[length] = (byte)'\n';
        length++;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                "00000009done\n"u8.CopyTo(memory.Span[length..]);
                length += 13;
            }

            disposedValue = true;
        }
    }


    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

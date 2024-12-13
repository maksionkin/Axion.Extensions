// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using Microsoft.Extensions.Caching.Hybrid;


namespace Axion.Extensions.Caching.Hybrid.Serialization.Http;

/// <summary>
/// A serializer for serializing items of type <see cref="HttpResponseMessage"/> for <see cref="HybridCache"/>
/// </summary>
public class HttpResponseMessageHybridCacheSerializer : IHybridCacheSerializer<HttpResponseMessage>
{
    static readonly Version DefaultVersion = new(0, 9);

    internal static readonly Encoding Utf8 = new UTF8Encoding(false);

    /// <summary>
    /// Gets an instance of <see cref="HttpResponseMessageHybridCacheSerializer"/>
    /// </summary>
    public static readonly HttpResponseMessageHybridCacheSerializer Instance = new();

    /// <inheritdoc/>>
    public HttpResponseMessage Deserialize(ReadOnlySequence<byte> source)
    {
        var currentPostion = source.Start;
        var nextPostion = currentPostion;
        var currentIndex = 0;
        var current = ReadOnlyMemory<byte>.Empty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool EnsureHasData()
        {
            while (currentIndex >= current.Length)
            {
                var saved = nextPostion;
                if (source.TryGet(ref nextPostion, out current))
                {
                    currentIndex = 0;
                    currentPostion = saved;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ReadConst(ReadOnlySpan<byte> value)
        {

            while (EnsureHasData())
            {
                var d = Math.Min(current.Span.Length - currentIndex, value.Length);

                if (current.Span.Slice(currentIndex, d).SequenceEqual(value[..d]))
                {
                    currentIndex += d;

                    if (d == value.Length)
                    {
                        return true;
                    }
                    else
                    {
                        value = value[d..];
                    }
                }
                else
                {
                    break;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ReadByte(out byte value)
        {
            if (EnsureHasData())
            {
                value = current.Span[currentIndex++];

                return true;
            }

            value = default;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ReadConstByte(byte value)
        {
            if (EnsureHasData() && current.Span[currentIndex] == value)
            {
                currentIndex++;

                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ReadDigit(out int value)
        {
            if (ReadByte(out var d) && d >= '0' && d <= '9')
            {
                value = d - '0';

                return true;
            }

            value = default;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ReadVersion(out Version value)
        {
            if (ReadDigit(out var major))
            {
                if (ReadConstByte((byte)'.') && ReadDigit(out var minor))
                {
                    value = new(major, minor);
                }
                else
                {
                    value = new(major, 0);
                }

                return true;
            }

            value = DefaultVersion;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ReadSpaces()
        {
            var read = false;
            while (EnsureHasData())
            {
                if (current.Span[currentIndex] == ' ')
                {
                    currentIndex++;
                    read = true;
                }
                else
                {
                    break;
                }
            }

            return read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ReadLine() =>
            ReadConst("\r\n"u8);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ReadStatus(out HttpStatusCode value)
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
        ReadOnlySequence<byte> Slice(int length) =>
            source.Slice(currentPostion).Slice(currentIndex, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ReadHex(out int value)
        {
            var read = false;
            value = 0;

            while (EnsureHasData())
            {
                var digit = current.Span[currentIndex];

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

                value <<= 4;
                value += d;

                currentIndex++;

                read = true;
            }

            return read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ReadChunckedBody(out ReadOnlySequence<byte> value)
        {
            var result = true;
            var firstSegment = new SettableReadOnlySequenceSegment<byte>();
            var currentSegment = firstSegment;

            while (result)
            {
                if (ReadHex(out var size) && ReadLine())
                {
                    if (size == 0)
                    {
                        result &= ReadLine();

                        break;
                    }

                    while (size > 0)
                    {
                        if (EnsureHasData())
                        {
                            var previousSegment = currentSegment;
                            currentSegment = new SettableReadOnlySequenceSegment<byte>()
                                .SetMemory(current.Slice(currentIndex, Math.Min(current.Length - currentIndex, size)))
                                .SetRunningIndex(previousSegment.RunningIndex + previousSegment.Memory.Length);

                            previousSegment?.SetNext(currentSegment);

                            currentIndex += currentSegment.Memory.Length;
                            size -= currentSegment.Memory.Length;
                        }
                        else
                        {
                            result = false;

                            break;
                        }
                    }

                    if (size == 0)
                    {
                        result = ReadLine();
                    }
                }
                else
                {
                    result = false;
                }
            }

            value = new ReadOnlySequence<byte>(firstSegment, 0, currentSegment, currentSegment.Memory.Length);

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ReadBody(long? length, out ReadOnlySequence<byte> value)
        {
            value = source.Slice(currentPostion).Slice(currentIndex);

            if (length == null || length > value.Length)
            {
                return false;
            }
            else
            {
                source = value.Slice(length.Value);
                currentPostion = source.Start;
                currentIndex = 0;
                nextPostion = currentPostion;

                value = value.Slice(0, length.Value);

                return source.TryGet(ref nextPostion, out current);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ReadString([NotNullWhen(true)] out string? value, byte delimiter = 0)
        {
            var position = currentPostion;
            var next = nextPostion;
            var memory = current;
            var index = currentIndex;
            var length = 0;

            while (true)
            {
                var end = false;
                while (index == memory.Length)
                {
                    position = next;
                    if (source.TryGet(ref next, out memory))
                    {
                        index = 0;
                    }
                    else
                    {
                        end = true;

                        break;
                    }
                }

                if (!end)
                {
                    var i = memory.Span[index..].IndexOfAny(delimiter, (byte)'\r', (byte)'\n');
                    if (i < 0)
                    {
                        length += memory.Length - index;
                        index = memory.Length;
                    }
                    else
                    {
                        length += i;
                        index += i;

                        end = true;
                    }
                }

                if (end)
                {
                    if (length == 0)
                    {
                        value = null;
                        return false;
                    }

                    var bytes = Slice(length);

#if NET5_0_OR_GREATER
                    value = Utf8.GetString(bytes);
#else
                    var buffer = ArrayPool<byte>.Shared.Rent(length);

                    try
                    {
                        bytes.CopyTo(buffer);

                        value = Utf8.GetString(buffer, 0, length);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
#endif

                    currentIndex = index;
                    current = memory;
                    currentPostion = position;
                    nextPostion = next;

                    return true;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerable<(string Header, string? Value)> ReadHeaders()
        {
            do
            {
                if (!ReadString(out var header, (byte)':') || header == null)
                {
                    break;
                }

                var hasValue = ReadConstByte((byte)':');

                string? value = null;
                if (hasValue)
                {
                    ReadString(out value);

                    value ??= "";
                }
           
                yield return (header.Trim(), value?.Trim());
            }
            while (ReadLine());
        }

        if (ReadConst("HTTP/"u8)
            && ReadVersion(out var version)
            && ReadSpaces()
            && ReadStatus(out var status))
        {
            ReadSpaces();

            ReadString(out var reason);

            var response = new HttpResponseMessage(status)
            {
                Version = version,
                ReasonPhrase = reason
            };

            response.Content ??= new StreamContent(Stream.Null);

            if (ReadLine())
            {
                long? contentLength = null;
                foreach (var (header, value) in ReadHeaders())
                {
                    if ("Content-Length".Equals(header, StringComparison.OrdinalIgnoreCase) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
                    {
                        contentLength = size;
                    }

                    if (!response.Headers.TryAddWithoutValidation(header, value))
                    {
                        response.Content.Headers.TryAddWithoutValidation(header, value);
                    }
                }

                if (ReadLine())
                {
                    bool trailingHeaders;
                    ReadOnlySequence<byte> body;
                    if (response.Headers.TransferEncodingChunked == true)
                    {
                        trailingHeaders = ReadChunckedBody(out body);
                    }
                    else if (contentLength == 0)
                    {
                        trailingHeaders = true;
                        body = ReadOnlySequence<byte>.Empty;
                    }
                    else
                    {
                        trailingHeaders = ReadBody(contentLength, out body);
                    }

                    if (body.Length > 0)
                    {
                        var content = new StreamContent(body.AsStream());

                        foreach (var header in response.Content.Headers)
                        {
                            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }

                        response.Content = content;
                    }

                    if (trailingHeaders)
                    {
                        foreach (var (header, value) in ReadHeaders())
                        {
                            response.
#if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                                TrailingHeaders
#else
                                Headers
#endif
                                .TryAddWithoutValidation(header, value);
                        }
                    }
                }
            }

            return response;
        }
        else
        {
            return new()
            {
                Version = DefaultVersion,
                Content = new StreamContent(source.AsStream())
            };
        }
    }

    /// <inheritdoc/>>
    public void Serialize(HttpResponseMessage value, IBufferWriter<byte> target)
    {
        target.Write("HTTP/"u8);

        target.Write(value.Version);
        target.Write(value.StatusCode);

        if (value.ReasonPhrase != null)
        {
            target.Write(value.ReasonPhrase ?? "");
        }

        target.WriteLine();
        target.Write(value.Headers);
        target.Write(value.Content.Headers);
        target.WriteLine();

        value.Content.LoadIntoBufferAsync().Wait();

        var contentStream = value.Content.ReadAsStreamAsync().Result;

        if (value.Headers.TransferEncodingChunked == true)
        {
            var span = target.SafeGetSpan();


            var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);

            try
            {
                while (true)
                {
                    var read = contentStream.Read(buffer, 0, buffer.Length);

                    target.WriteHex(read);
                    target.WriteLine();

                    if (read == 0)
                    {
                        break;
                    }

                    target.Write(new ReadOnlySpan<byte>(buffer, 0, read));

                    target.WriteLine();
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            target.WriteLine();
        }
        else
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            while (true)
            {
                var span = target.SafeGetSpan();
                var read = contentStream.Read(span);
                if (read == 0)
                {
                    break;
                }

                target.Advance(read);
            }
#else
            var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);

            try
            {
                while (true)
                {
                    var read = contentStream.Read(buffer, 0, buffer.Length);

                    if (read == 0)
                    {
                        break;
                    }

                    target.Write(new ReadOnlySpan<byte>(buffer, 0, read));
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
#endif
        }

#if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        target.Write(value.TrailingHeaders);
#endif
    }
}

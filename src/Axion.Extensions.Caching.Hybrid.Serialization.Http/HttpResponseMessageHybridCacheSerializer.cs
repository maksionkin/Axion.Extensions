// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using Microsoft.Extensions.Caching.Hybrid;


namespace Axion.Extensions.Caching.Hybrid.Serialization.Http;

/// <summary>
/// A serializer for serializing items of type <see cref="HttpResponseMessage"/> for <see cref="HybridCache"/>
/// </summary>
public class HttpResponseMessageHybridCacheSerializer(HttpResponseMessageHybridCacheSerializer.Options? options = null) : IHybridCacheSerializer<HttpResponseMessage>
{
    static readonly Version DefaultVersion = new(0, 9);

    readonly Options options = options ?? Options.Default;

    /// <summary>
    /// Gets an instance of <see cref="HttpResponseMessageHybridCacheSerializer"/>
    /// </summary>
    public static readonly HttpResponseMessageHybridCacheSerializer Instance = new();

    /// <summary>
    /// Creates a new instance of <see cref="HttpResponseMessageHybridCacheSerializer"/>.
    /// </summary>
    public HttpResponseMessageHybridCacheSerializer() : this(null) 
    { 
    }

    /// <inheritdoc/>>
    public HttpResponseMessage Deserialize(ReadOnlySequence<byte> source)
    {
        var reader = new HttpResponseMessageReader(source, options);

        if (reader.Read("HTTP/"u8)
            && reader.Read(out Version? version)
            && reader.ReadSpace()
            && reader.Read(out HttpStatusCode status))
        {
            reader.ReadSpace();

            var response = new HttpResponseMessage(status)
            {
                Version = version,
                ReasonPhrase = reader.ReadString(),
            };

            response.Content ??= new StreamContent(Stream.Null);

            if (reader.ReadLine())
            {
                long? contentLength = null;
                while (reader.ReadHeader(out var header, out var value))
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

                if (reader.ReadLine())
                {
                    var content = new StreamContent(response.Headers.TransferEncodingChunked == true ? reader.ReadChuncked() : reader.ReadBody(contentLength));

                    foreach (var header in response.Content.Headers)
                    {
                        content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }

                    response.Content = content;

                    while (reader.ReadHeader(out var header, out var value))
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
        var writer = new HttpResponseMessageWriter(target);

        writer.Write("HTTP"u8);

        writer.Write(value.Version);
        writer.Write(value.StatusCode);

        if (value.ReasonPhrase != null)
        {
            writer.Write((byte)' ');

            writer.Write(value.ReasonPhrase.AsSpan());
        }

        writer.WriteLine();
        writer.Write(value.Headers);
        writer.Write(value.Content.Headers);
        writer.WriteLine();

        value.Content.LoadIntoBufferAsync().Wait();

        var contentStream = value.Content.ReadAsStreamAsync().Result;

        if (value.Headers.TransferEncodingChunked == true)
        {
            writer.WriteChuncked(contentStream);
        }
        else
        {
            writer.Write(contentStream);
        }

#if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        writer.Write(value.TrailingHeaders);
#endif

        writer.Flush();
    }

    /// <summary>
    /// <see cref="HttpResponseMessageHybridCacheSerializer"/> options;
    /// </summary>
    public class Options
    {
        /// <summary>
        /// Default <see cref="HttpResponseMessageHybridCacheSerializer"/> options.
        /// </summary>
        public static readonly Options Default = new();

        /// <summary>
        /// Gets or sets the <see cref="ArrayPool{Char}"/> for array allocations.
        /// </summary>
        public ArrayPool<char> CharArrayPool
        {
            get;
            set
            {
                Guard.IsNotNull(value);

                field = value;
            }
        } = ArrayPool<char>.Shared;

        /// <summary>
        /// Gets or sets the <see cref="StringPool"/> for strng allocations.
        /// </summary>
        public StringPool StringPool
        {
            get;
            set
            {
                Guard.IsNotNull(value);

                field = value;
            }
        } = StringPool.Shared;

        /// <summary>
        /// Gets or sets max char count to be stack allocated.
        /// </summary>
        public int MaxCharsOnStack
        {
            get; 
            set
            {
                Guard.IsGreaterThanOrEqualTo(value, 0);

                field = value;
            }
        } = 32;
    }
}

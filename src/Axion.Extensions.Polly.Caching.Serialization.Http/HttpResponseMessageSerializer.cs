// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Axion.Extensions.Caching.Hybrid.Serialization.Http;
using Microsoft.IO;
using Polly.Caching;

namespace Axion.Extensions.Polly.Caching.Serialization.Http;

/// <summary>
/// A serializer for serializing items of type <see cref="HttpResponseMessage"/>, for the Polly <see cref="CachePolicy"/>
/// </summary>
public class HttpResponseMessageSerializer : ICacheItemSerializer<HttpResponseMessage, byte[]>
{
    static readonly RecyclableMemoryStreamManager RecyclableMemoryStreamManager = new();

    /// <summary>
    /// Gets an instance of <see cref="HttpResponseMessageSerializer"/>
    /// </summary>
    public static readonly HttpResponseMessageSerializer Instance = new();

    /// <inheritdoc/>
    public HttpResponseMessage Deserialize(byte[] objectToDeserialize)
    {
        ArgumentNullException.ThrowIfNull(objectToDeserialize);

        return HttpResponseMessageHybridCacheSerializer.Instance.Deserialize(new(objectToDeserialize));
    }

    /// <inheritdoc/>
    public byte[] Serialize(HttpResponseMessage objectToSerialize)
    {
        ArgumentNullException.ThrowIfNull(objectToSerialize);

        using var ms = RecyclableMemoryStreamManager.GetStream();
        HttpResponseMessageHybridCacheSerializer.Instance.Serialize(objectToSerialize, ms);

        return ms.ToArray();
    }
}

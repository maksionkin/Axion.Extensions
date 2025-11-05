// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Axion.Extensions.Caching.Transformed;

/// <summary>
/// Configuration options for <see cref="TransformedCache"/>.
/// </summary>
public class TransformedCacheOptions : IOptions<TransformedCacheOptions>
{
    Func<string, string> convertCacheKey;


    /// <summary>
    /// Initializes a new instance of <see cref="TransformedCache"/>.
    /// </summary>
    public TransformedCacheOptions() =>
        convertCacheKey = Id;

    static string Id(string key) =>
        key;

    /// <summary>
    /// Cache key converter.
    /// </summary>
    public Func<string, string> ConvertCacheKey
    {
        get => convertCacheKey;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            convertCacheKey = value;
        }
    }

    /// <summary>
    /// Encoding transform.
    /// </summary>
    public required Func<byte[], byte[]> Encode { get; set; }

    /// <summary>
    /// Decoding transform.
    /// </summary>
    public required Func<byte[], byte[]> Decode { get; set; }

    /// <summary>
    /// A wrapped <see cref="IDistributedCache"/> provider.
    /// </summary>
    public required IDistributedCache CacheProvider { get; set; }

    TransformedCacheOptions IOptions<TransformedCacheOptions>.Value =>
        this;
}

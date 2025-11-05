// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Axion.Extensions.Caching.Transformed;

/// <summary>
/// Distributed cache implementation with a transform.
/// </summary>
public class TransformedCache : IDistributedCache
{
    readonly IOptions<TransformedCacheOptions> optionsAccessor;

    /// <summary>
    /// Initializes a new instance of <see cref="TransformedCache"/>.
    /// </summary>
    /// <param name="optionsAccessor">The configuration options.</param>
    public TransformedCache(IOptions<TransformedCacheOptions> optionsAccessor)
    {
        ArgumentNullException.ThrowIfNull(optionsAccessor);

        this.optionsAccessor = optionsAccessor;
    }

    /// <inheritdoc/>
    public byte[]? Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(optionsAccessor.Value.Decode);
        ArgumentNullException.ThrowIfNull(optionsAccessor.Value.CacheProvider);

        var result = optionsAccessor.Value.CacheProvider.Get(optionsAccessor.Value.ConvertCacheKey(key));

        return result == null
            ? null
            : optionsAccessor.Value.Decode(result);
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(optionsAccessor.Value.Decode);
        ArgumentNullException.ThrowIfNull(optionsAccessor.Value.CacheProvider);

        var result = await optionsAccessor.Value.CacheProvider.GetAsync(optionsAccessor.Value.ConvertCacheKey(key), token);

        return result == null
            ? null
            : optionsAccessor.Value.Decode(result);
    }

    /// <inheritdoc/>
    public void Refresh(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(optionsAccessor.Value.CacheProvider);

        optionsAccessor.Value.CacheProvider.Refresh(optionsAccessor.Value.ConvertCacheKey(key));
    }

    /// <inheritdoc/>
    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(optionsAccessor.Value.CacheProvider);

        await optionsAccessor.Value.CacheProvider.RefreshAsync(optionsAccessor.Value.ConvertCacheKey(key), token);
    }

    /// <inheritdoc/>
    public void Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(optionsAccessor.Value.CacheProvider);

        optionsAccessor.Value.CacheProvider.Remove(optionsAccessor.Value.ConvertCacheKey(key));
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(optionsAccessor.Value.CacheProvider);

        await optionsAccessor.Value.CacheProvider.RemoveAsync(optionsAccessor.Value.ConvertCacheKey(key), token);
    }


    /// <inheritdoc/>
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(optionsAccessor.Value.Encode);
        ArgumentNullException.ThrowIfNull(optionsAccessor.Value.CacheProvider);

        optionsAccessor.Value.CacheProvider.Set(optionsAccessor.Value.ConvertCacheKey(key), optionsAccessor.Value.Encode(value), options);
    }

    /// <inheritdoc/>
    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(optionsAccessor.Value.Encode);
        ArgumentNullException.ThrowIfNull(optionsAccessor.Value.CacheProvider);

        await optionsAccessor.Value.CacheProvider.SetAsync(optionsAccessor.Value.ConvertCacheKey(key), optionsAccessor.Value.Encode(value), options, token);
    }
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using Axion.Extensions.Polly.Caching.Hybrid;
using Axion.Extensions.Polly.DuplicateRequestCollapsing.Caching.Hybrid;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;

#pragma warning disable IDE0130	// Namespace  does not match folder structure
namespace Polly;

/// <summary>
/// Extension methods for <see cref="ResiliencePipelineBuilder{TResult}"/>.
/// </summary>
public static class PipelineBuilderExtensions
{
    /// <summary>
    /// Adds a duplicate request collapsing to the builder.
    /// </summary>
    /// <typeparam name="TResult">The type of result the duplicate request collapsing handles.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="options">The duplicate request collapsing options.</param>
    /// <returns>The builder instance with the duplicate request collapsing added.</returns>
    public static ResiliencePipelineBuilder<TResult> AddDuplicateRequestCollapsing<TResult>(
        this ResiliencePipelineBuilder<TResult> builder,
        DuplicateRequestCollapsingResilienceStrategyOptions<TResult> options)
    {
        Guard.IsNotNull(builder);
        Guard.IsNotNull(options);

        var cacheOptions = new CachingStrategyOptions<TResult>()
        {
            CacheKeyProvider = options.CacheKeyProvider,
            HybridCache = options.HybridCache,
            HybridCacheSetEntryOptionsProvider = 
                async (context, result) =>
                {
                    var o = await options.HybridCacheSetEntryOptionsProvider(context, result);

                    return new()
                    {
                        Expiration = o?.Expiration,
                        LocalCacheExpiration = o?.LocalCacheExpiration,
                        Flags = (o?.Flags).GetValueOrDefault() | HybridCacheEntryFlags.DisableDistributedCacheWrite | HybridCacheEntryFlags.DisableLocalCacheWrite,
                    };
                },
            OnCacheHit = options.OnCacheHit,
            OnCacheMiss = options.OnCacheMiss,
            OnCacheReadError = options.OnCacheReadError,
            OnCacheWriteError = options.OnCacheWriteError,
        };

        var lockOptions = new LockResilienceStrategyOptions()
        {
            LockHandleProvider = async context => await options.LockHandleProvider(context, await options.CacheKeyProvider(context))
        };

        return builder.AddCaching(cacheOptions)
            .AddStrategy(
                _ => new LockResilienceStrategy(lockOptions),
                options)
            .AddCaching(options);
    }
}

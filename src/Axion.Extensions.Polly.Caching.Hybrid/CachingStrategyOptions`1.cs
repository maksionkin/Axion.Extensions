// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;
using Polly;

namespace Axion.Extensions.Polly.Caching.Hybrid;

/// <summary>
/// Represents the options used to configure a caching strategy.
/// </summary>
/// <typeparam name="TResult">The type of result the retry strategy handles.</typeparam>
public class CachingStrategyOptions<TResult> : ResilienceStrategyOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CachingStrategyOptions{TResult}"/> class.
    /// </summary>
    public CachingStrategyOptions() =>
        Name = "Caching";

    /// <summary>
    /// Gets and sets an instance of <see cref="HybridCache"/> used for caching.
    /// </summary>
    [Required]
    public required HybridCache HybridCache { get; set; }

    /// <summary>
    /// Gets or sets a provider to get a <see cref="HybridCacheEntryFlags"/> to get a cached value.
    /// </summary>
    [Required]
    public Func<ResilienceContext, ValueTask<HybridCacheEntryFlags?>> HybridCacheGetFlagsProvider { get; set; } = DefaultProviders.HybridCacheGetFlagsProvider;

    /// <summary>
    /// Gets or sets a provider to get a cache key otherwise <see cref="ResilienceContext.OperationKey"/> is used.
    /// </summary>
    [Required]
    public Func<ResilienceContext, ValueTask<string>> CacheKeyProvider { get; set; } = DefaultProviders.CacheKeyProvider;

    /// <summary>
    /// Gets or sets a provider to get a <see cref="HybridCacheEntryOptions"/> to set the cached value.
    /// </summary>
    [Required]
    public Func<ResilienceContext, TResult, ValueTask<HybridCacheEntryOptions?>> HybridCacheSetEntryOptionsProvider { get; set; } = DefaultProviders.HybridCacheSetEntryOptionsProvider;

    /// <summary>
    /// Gets or sets a provider to get cache tags.
    /// </summary>
    [Required]
    public Func<ResilienceContext, TResult, ValueTask<IEnumerable<string>?>> CacheEntryTagsProvider { get; set; } = DefaultProviders.CacheEntryTagsProvider;

    /// <summary>
    /// Gets or sets a delegate that is called when an item is fetched from the cache.
    /// </summary>
    public Func<ResilienceContext, OnCacheHitArgument<TResult>, ValueTask>? OnCacheHit { get; set; }

    /// <summary>
    /// Gets or sets a delegate that is called when an item is missing in the cache.
    /// </summary>
    public Func<ResilienceContext, CacheKeyArgument, ValueTask>? OnCacheMiss { get; set; }

    /// <summary>
    /// Gets or sets a delegate that is called when a cache reading error occurs.
    /// </summary>
    public Func<ResilienceContext, CacheKeyAndExceptionArgument, ValueTask>? OnCacheReadError { get; set; }

    /// <summary>
    /// Gets or sets a delegate that is called when a cache writing error occurs.
    /// </summary>
    public Func<ResilienceContext, OnCacheWriteErrorArgument<TResult>, ValueTask>? OnCacheWriteError { get; set; }

}

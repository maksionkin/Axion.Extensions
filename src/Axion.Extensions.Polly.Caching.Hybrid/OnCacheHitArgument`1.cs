// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

namespace Axion.Extensions.Polly.Caching.Hybrid;

/// <summary>
/// An argument when an entry is fetched from the cache.
/// </summary>
/// <typeparam name="TResult">The type of result the retry strategy handles.</typeparam>
/// <param name="CacheKey">The cache key of the entry.</param>
/// <param name="Result">The entry that was read from the cache.</param>
public readonly record struct OnCacheHitArgument<TResult>(string CacheKey, TResult Result);

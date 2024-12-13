// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Axion.Extensions.Polly.Caching.Hybrid;

/// <summary>
/// An argument when a write error occured while writing to the cache.
/// </summary>
/// <typeparam name="TResult">The type of result the retry strategy handles.</typeparam>
/// <param name="CacheKey">The cache key of the entry.</param>
/// <param name="Result">The entry that was attempted to be writted to the cache.</param>
/// <param name="Exception">The <see cref="Exception"/> occured.</param>
public readonly record struct OnCacheWriteErrorArgument<TResult>(string CacheKey, TResult Result, Exception Exception);

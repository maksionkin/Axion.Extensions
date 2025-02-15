// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

namespace Axion.Extensions.Polly.Caching.Hybrid;

/// <summary>
/// An argument that contains a cache key.
/// </summary>
/// <param name="CacheKey">The cache key of the entry.</param>
public readonly record struct CacheKeyArgument(string CacheKey);

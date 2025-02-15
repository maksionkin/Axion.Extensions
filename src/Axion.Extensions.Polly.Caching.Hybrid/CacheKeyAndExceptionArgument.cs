// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Axion.Extensions.Polly.Caching.Hybrid;

/// <summary>
/// An argument that contains a cache key ae.
/// </summary>
/// <param name="CacheKey">The cache key of the entry.</param>
/// <param name="Exception">The <see cref="Exception"/> occured.</param>
public readonly record struct CacheKeyAndExceptionArgument(string CacheKey, Exception Exception);


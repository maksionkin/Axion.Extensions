// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http;
using Axion.Extensions.Polly.DuplicateRequestCollapsing.Caching.Hybrid;

namespace Axion.Extensions.Http.Resilience;

/// <summary>
/// Implementation of the <see cref="DuplicateRequestCollapsingResilienceStrategyOptions{HttpResponseMessage}"/> for <see cref="HttpResponseMessage"/> results.
/// </summary>
public class HttpDuplicateRequestCollapsingStrategyOptions : DuplicateRequestCollapsingResilienceStrategyOptions<HttpResponseMessage>
{
    static readonly HttpCachingStrategyOptions Options = new() { HybridCache = null! };

    /// <summary>
    /// Creates an instance of <see cref="HttpDuplicateRequestCollapsingStrategyOptions"/>. 
    /// </summary>
    public HttpDuplicateRequestCollapsingStrategyOptions()
    {
        CacheKeyProvider = Options.CacheKeyProvider;
        HybridCacheSetEntryOptionsProvider = Options.HybridCacheSetEntryOptionsProvider;
        HybridCacheGetFlagsProvider = Options.HybridCacheGetFlagsProvider;
        OnCacheHit = Options.OnCacheHit;
    }
}

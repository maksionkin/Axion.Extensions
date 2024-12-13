// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http;
using Axion.Extensions.Polly.Caching.Hybrid;

namespace Axion.Extensions.Http.Resilience;

/// <summary>
/// Implementation of the <see cref="CachingStrategyOptions{HttpResponseMessage}"/> for <see cref="HttpResponseMessage"/> results.
/// </summary>
public class HttpCachingStrategyOptions : CachingStrategyOptions<HttpResponseMessage>
{
    //
    // Summary:
    //     Initializes a new instance of the Microsoft.Extensions.Http.Resilience.HttpRetryStrategyOptions
    //     class.
    //
    // Remarks:
    //     By default, the options are configured to handle only transient failures. Specifically,
    //     this includes HTTP status codes 408, 429, 500 and above, as well as System.Net.Http.HttpRequestException
    //     and Polly.Timeout.TimeoutRejectedException exceptions
    /// <summary>
    /// Initializes a new instance of the <see cref="HttpCachingStrategyOptions"/>.
    /// </summary>
    /// <remarks>By default the options are confgured to cache responses of GET and HEAD requests that completed with cacheale status codes (200, 203, 204, 206, 300, 301, 404, 405, 410, 414, and 501) https://developer.mozilla.org/en-US/docs/Glossary/Cacheable.</remarks>
    public HttpCachingStrategyOptions()
    {
        CacheKeyProvider = DefaultProviders.CacheKeyProvider;
        HybridCacheSetEntryOptionsProvider = DefaultProviders.HybridCacheSetEntryOptionsProvider;
        HybridCacheGetFlagsProvider = DefaultProviders.HybridCacheGetFlagsProvider;
        OnCacheHit = DefaultProviders.OnCacheHit;
    }
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Axion.Extensions.Polly.Caching.Hybrid;
using Microsoft.Extensions.Caching.Hybrid;
using Polly;

namespace Axion.Extensions.Http.Resilience;

static class DefaultProviders
{
    // https://developer.mozilla.org/en-US/docs/Glossary/Cacheable
    static readonly HttpStatusCode[] cacheableStatusCodes =
    [
        HttpStatusCode.OK,
        HttpStatusCode.NonAuthoritativeInformation,
        HttpStatusCode.NoContent,
        HttpStatusCode.PartialContent,
        HttpStatusCode.MultipleChoices,
        HttpStatusCode.MovedPermanently,
        HttpStatusCode.NotFound,
        HttpStatusCode.MethodNotAllowed,
        HttpStatusCode.Gone,
        HttpStatusCode.RequestUriTooLong,
        HttpStatusCode.NotImplemented
    ];

    static readonly HttpMethod[] cacheableMethods = [HttpMethod.Get, HttpMethod.Head];

    public static ValueTask<string> CacheKeyProvider(ResilienceContext context)
    {
        var message = context.GetRequestMessage() ?? throw new InvalidOperationException();

        if (message.RequestUri == null)
        {
            throw new InvalidOperationException();
        }

        var key = new StringBuilder(message.Method.Method.ToLowerInvariant()).Append('/');

        if (message.RequestUri.IsAbsoluteUri)
        {
            key.Append(message.RequestUri.Scheme)
                .Append('/')
                .Append(message.RequestUri.IdnHost);

            if (!message.RequestUri.IsDefaultPort)
            {
                key.Append(':')
                    .Append(message.RequestUri.Port);
            }
        }
        else
        {
            key.Append('-');
        }

        if (message.RequestUri.LocalPath.Length > 1)
        {
            key.Append(message.RequestUri.LocalPath);
        }

        return new ValueTask<string>(key.ToString());
    }

    public static ValueTask<HybridCacheEntryFlags?> HybridCacheGetFlagsProvider(ResilienceContext context)
    {
        var method = context.GetRequestMessage()?.Method;

        return new(method == null || cacheableMethods.Contains(method)
            ? null
            : HybridCacheEntryFlags.DisableLocalCacheRead | HybridCacheEntryFlags.DisableLocalCacheRead);
    }

    public static ValueTask<HybridCacheEntryOptions?> HybridCacheSetEntryOptionsProvider(ResilienceContext _, HttpResponseMessage result) =>
        new(new HybridCacheEntryOptions()
        {
            Flags = Array.BinarySearch(cacheableStatusCodes, result.StatusCode) >= 0
                ? null
                : HybridCacheEntryFlags.DisableLocalCacheWrite | HybridCacheEntryFlags.DisableLocalCacheWrite
        });

    public static ValueTask OnCacheHit(ResilienceContext context, OnCacheHitArgument<HttpResponseMessage> argument)
    {
        argument.Result.RequestMessage ??= context.GetRequestMessage();

        return default;
    }

}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Polly;

namespace Axion.Extensions.Polly.Caching.Hybrid;

static class DefaultProviders
{
    public static ValueTask<string> CacheKeyProvider(ResilienceContext context)
    {
        Guard.IsNotNull(context.OperationKey);

        return new ValueTask<string>(context.OperationKey);
    }

    public static ValueTask<HybridCacheEntryFlags?> HybridCacheGetFlagsProvider(ResilienceContext _) =>
        GetNullValueTask<HybridCacheEntryFlags?>();

    public static ValueTask<HybridCacheEntryOptions?> HybridCacheSetEntryOptionsProvider<TResult>(ResilienceContext _, TResult _1) =>
        GetNullValueTask<HybridCacheEntryOptions?>();

    public static ValueTask<IEnumerable<string>?> CacheEntryTagsProvider<TResult>(ResilienceContext _, TResult _1) =>
        GetNullValueTask<IEnumerable<string>?>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ValueTask<T> GetNullValueTask<T>() =>
        new((T)default!);
}

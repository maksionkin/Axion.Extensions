// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;
using Polly;
using Polly.Telemetry;

namespace Axion.Extensions.Polly.Caching.Hybrid;

class CachingResilienceStrategy<TResult>(CachingStrategyOptions<TResult> options, ResilienceStrategyTelemetry telemetry) : ResilienceStrategy<TResult>
{
    static readonly ResilienceEvent CacheHit = new(ResilienceEventSeverity.Debug, nameof(CacheHit));
    static readonly ResilienceEvent CacheMissed = new(ResilienceEventSeverity.Information, nameof(CacheMissed));
    static readonly ResilienceEvent CacheReadError = new(ResilienceEventSeverity.Warning, nameof(CacheReadError));
    static readonly ResilienceEvent CacheWriteError = new(ResilienceEventSeverity.Error, nameof(CacheWriteError));

    protected override async ValueTask<Outcome<TResult>> ExecuteCore<TState>(Func<ResilienceContext, TState, ValueTask<Outcome<TResult>>> callback, ResilienceContext context, TState state)
    {
        var key = await options.CacheKeyProvider(context).ConfigureAwait(context.ContinueOnCapturedContext);

        var getCacheItemFlags = (await options.HybridCacheGetFlagsProvider(context).ConfigureAwait(context.ContinueOnCapturedContext))
                .GetValueOrDefault();

        var getCacheItemOptions = new HybridCacheEntryOptions()
        {
            Flags = getCacheItemFlags
                | HybridCacheEntryFlags.DisableUnderlyingData
                | HybridCacheEntryFlags.DisableLocalCacheWrite
                | HybridCacheEntryFlags.DisableDistributedCacheWrite
        };

        TResult? cachedItem = default;

        try
        {
            cachedItem = await options.HybridCache.GetOrCreateAsync(key, Call, getCacheItemOptions, cancellationToken: context.CancellationToken).ConfigureAwait(context.ContinueOnCapturedContext);
        }
        catch (Exception e)
        {
            var cacheReadErrorArgument = new CacheKeyAndExceptionArgument(key, e);
            telemetry.Report(CacheReadError, context, cacheReadErrorArgument);

            var onCacheReadError = options.OnCacheReadError;
            if (onCacheReadError != null)
            {
                await onCacheReadError(context, cacheReadErrorArgument).ConfigureAwait(context.ContinueOnCapturedContext);
            }
        }

        if (getCacheItemFlags.HasFlag(HybridCacheEntryFlags.DisableUnderlyingData) || !Equals(cachedItem, default))
        {
            var cachedOutcome = Outcome.FromResult(cachedItem);

            telemetry.Report(CacheHit, context, cachedOutcome, new CacheKeyArgument(key));

            var onCacheHit = options.OnCacheHit;
            if (onCacheHit != null)
            {
                await onCacheHit(context, new(key, cachedItem!)).ConfigureAwait(context.ContinueOnCapturedContext);
            }

            return Outcome.FromResult(cachedItem);
        }

        var cacheMissArgument = new CacheKeyArgument(key);
        telemetry.Report(CacheMissed, context, cacheMissArgument);

        var onCacheMiss = options.OnCacheMiss;
        if (onCacheMiss != null)
        {
            await onCacheMiss(context, cacheMissArgument).ConfigureAwait(context.ContinueOnCapturedContext);
        }

        var outcome = await callback(context, state).ConfigureAwait(context.ContinueOnCapturedContext);
        if (outcome.Exception == null)
        {
            var setCacheItemOptions = options.HybridCacheSetEntryOptionsProvider(context, outcome.Result!).ConfigureAwait(context.ContinueOnCapturedContext);
            var cachetemTags = options.CacheEntryTagsProvider(context, outcome.Result!).ConfigureAwait(context.ContinueOnCapturedContext);

            try
            {
                await options.HybridCache.SetAsync(
                    key,
                    outcome.Result!,
                    await setCacheItemOptions,
                    await cachetemTags,
                    context.CancellationToken).ConfigureAwait(context.ContinueOnCapturedContext);
            }
            catch (Exception e)
            {
                telemetry.Report(CacheWriteError, context, outcome, new CacheKeyAndExceptionArgument(key, e));

                var onCacheWriteError = options.OnCacheWriteError;
                if (onCacheWriteError != null)
                {
                    await onCacheWriteError(context, new(key, outcome.Result!, e));
                }
            }
        }

        return outcome;
    }

    ValueTask<TResult> Call(CancellationToken cancellationToken) =>
        throw new InvalidOperationException();
}

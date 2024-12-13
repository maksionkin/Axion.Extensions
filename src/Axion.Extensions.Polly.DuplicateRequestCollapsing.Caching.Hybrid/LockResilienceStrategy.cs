// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Polly;

namespace Axion.Extensions.Polly.DuplicateRequestCollapsing.Caching.Hybrid;

class LockResilienceStrategy(LockResilienceStrategyOptions options) : ResilienceStrategy
{
    protected override async ValueTask<Outcome<TResult>> ExecuteCore<TResult, TState>(Func<ResilienceContext, TState, ValueTask<Outcome<TResult>>> callback, ResilienceContext context, TState state)
    {
        await using var handle = await options.LockHandleProvider(context);

        return await callback(context, state).ConfigureAwait(context.ContinueOnCapturedContext);
    }
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Polly;

namespace Axion.Extensions.Polly.DuplicateRequestCollapsing.Caching.Hybrid;

class LockResilienceStrategyOptions : ResilienceStrategyOptions
{
    public LockResilienceStrategyOptions() =>
        Name = "Lock";

    public required Func<ResilienceContext, ValueTask<IAsyncDisposable?>> LockHandleProvider { get; set; }
}

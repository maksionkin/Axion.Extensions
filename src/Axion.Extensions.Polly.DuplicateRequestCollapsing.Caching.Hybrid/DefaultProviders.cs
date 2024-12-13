// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Polly;

namespace Axion.Extensions.Polly.DuplicateRequestCollapsing.Caching.Hybrid;

static class DefaultProviders
{
    public static ValueTask<TimeSpan?> LockTimeoutProvider(ResilienceContext _) =>
        new ValueTask<TimeSpan?>((TimeSpan?)null);
}

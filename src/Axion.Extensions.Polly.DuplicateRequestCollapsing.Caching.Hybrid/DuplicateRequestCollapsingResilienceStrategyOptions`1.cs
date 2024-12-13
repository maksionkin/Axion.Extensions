// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Axion.Extensions.Polly.Caching.Hybrid;
using Polly;

namespace Axion.Extensions.Polly.DuplicateRequestCollapsing.Caching.Hybrid;

/// <summary>
/// Represents the options used to configure a duplicated request collapsing.
/// </summary>
/// <typeparam name="TResult">The type of result the retry strategy handles.</typeparam>
public class DuplicateRequestCollapsingResilienceStrategyOptions<TResult> : CachingStrategyOptions<TResult>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CachingStrategyOptions{TResult}"/> class.
    /// </summary>
    public DuplicateRequestCollapsingResilienceStrategyOptions() =>
        Name = "DuplicateRequestCollapsing";

    /// <summary>
    /// Gets or sets a a lock provider.
    /// A provder should return <see langword="null"/> if no lock is needed.
    /// </summary>
    [Required]
    public required Func<ResilienceContext, string, ValueTask<IAsyncDisposable?>> LockHandleProvider { get; set; }
}

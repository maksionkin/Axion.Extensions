// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using Axion.Extensions.Polly.Caching.Hybrid;
using CommunityToolkit.Diagnostics;


#pragma warning disable IDE0130	// Namespace  does not match folder structure
namespace Polly;

/// <summary>
/// Extension methods for <see cref="ResiliencePipelineBuilder{TResult}"/>.
/// </summary>
public static class PipelineBuilderExtensions
{
    /// <summary>
    /// Adds a caching to the builder.
    /// </summary>
    /// <typeparam name="TResult">The type of result the caching handles.</typeparam>
    /// <param name="builder">The builder instance.</param>
    /// <param name="options">The caching options.</param>
    /// <returns>The builder instance with the caching added.</returns>
    public static ResiliencePipelineBuilder<TResult> AddCaching<TResult>(
        this ResiliencePipelineBuilder<TResult> builder,
        CachingStrategyOptions<TResult> options)
    {
        Guard.IsNotNull(builder);
        Guard.IsNotNull(options);

        return builder.AddStrategy(
            context => new CachingResilienceStrategy<TResult>(options, context.Telemetry),
            options);
    }
}

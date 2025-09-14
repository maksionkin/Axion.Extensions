// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using Axion.Extensions.Caching.Hybrid.Serialization.Http;
using Microsoft.Extensions.Caching.Hybrid;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IHybridCacheBuilder"/>.
/// </summary>
public static class HybridCacheBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="HttpResponseMessageHybridCacheSerializer"/> to <see cref="IHybridCacheBuilder"/>.
    /// </summary>
    /// <param name="builder">A <see cref="IHybridCacheBuilder"/>.</param>
    /// <param name="options">The options for the <see cref="HttpResponseMessageHybridCacheSerializer"/>.</param>
    /// <returns>The <see cref="IHybridCacheBuilder"/> instance.</returns>
    public static IHybridCacheBuilder AddHttpResponseMessageSerializer(this IHybridCacheBuilder builder, HttpResponseMessageHybridCacheSerializer.Options? options = null) =>
        builder.AddSerializer(options == null ? HttpResponseMessageHybridCacheSerializer.Instance : new(options));


}

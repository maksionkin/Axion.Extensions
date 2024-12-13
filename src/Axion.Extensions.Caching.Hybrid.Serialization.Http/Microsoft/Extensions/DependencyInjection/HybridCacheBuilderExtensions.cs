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
#pragma warning disable EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    /// <summary>
    /// Adds <see cref="HttpResponseMessageHybridCacheSerializer"/> to <see cref="IHybridCacheBuilder"/>.
    /// </summary>
    /// <param name="builder">A <see cref="IHybridCacheBuilder"/>.</param>
    /// <returns>The <see cref="IHybridCacheBuilder"/> instance.</returns>
    public static IHybridCacheBuilder AddHttpResponseMessageSerializer(this IHybridCacheBuilder builder) =>
        builder.AddSerializer(HttpResponseMessageHybridCacheSerializer.Instance);
#pragma warning restore EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
}

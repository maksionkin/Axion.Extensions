// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using Axion.Extensions.Caching.Azure.Storage.Blobs;
using Dawn;
using Microsoft.Extensions.Caching.Distributed;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up Redis distributed cache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class AzureBlobsCacheServiceCollectionExtensions
{
    /// <summary>
    /// Adds Azure Blobs distributed caching services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="setupAction">An <see cref="Action{AzureBlobsCacheOptions}"/> to configure the provided
    /// <see cref="AzureBlobsCacheOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddStackExchangeRedisCache(this IServiceCollection services, Action<AzureBlobsCacheOptions> setupAction)
    {

        Guard.Argument(services).NotNull().Value.AddOptions();

        services.Configure(Guard.Argument(setupAction).NotNull().Value);
        services.Add(ServiceDescriptor.Singleton<IDistributedCache, AzureBlobsCache>());

        return services;
    }
}

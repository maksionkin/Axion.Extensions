// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using Axion.Extensions.Caching.Azure.Storage.Blobs;
using Azure.Storage.Blobs;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up Azure Blob distributed cache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class AzureBlobsCacheServiceCollectionExtensions
{
    /// <summary>
    /// Adds Azure Blobs distributed caching services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="setupAction">An <see cref="Action{AzureBlobsCacheOptions}"/> to configure the provided <see cref="AzureBlobsCacheOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddAzureBlobCache(this IServiceCollection services, Action<AzureBlobsCacheOptions> setupAction)
    {
        Guard.IsNotNull(services);
        Guard.IsNotNull(setupAction);

        var descriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(BlobServiceClient));
        if (descriptor == null)
        {
            services.AddOptions();

            services.Configure(setupAction);

            return services.AddSingleton<IDistributedCache, AzureBlobsCache>();
        }
        else
        {
            services.Add(new(typeof(IDistributedCache),
                serviceProvider =>
                {
                    var options = new AzureBlobsCacheOptions();

                    options.GetBlobContainerClient = () => serviceProvider.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(options.ContainerName);

                    setupAction(options);

                    return new AzureBlobsCache(options);
                },
                descriptor.Lifetime));

            return services;
        }
    }
}

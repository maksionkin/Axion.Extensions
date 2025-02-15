// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using Axion.Extensions.Caching.Azure.Storage.Blobs;
using Azure.Storage.Blobs;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130	// Namespace  does not match folder structure
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
    public static IServiceCollection AddAzureBlobCache(this IServiceCollection services, Action<AzureBlobsCacheOptions>? setupAction = null)
    {
        return services.AddAzureBlobCache((options, provider) =>
        {
            if (string.IsNullOrEmpty(options.ConnectionString) && options.UsesDefaultGetBlobContainerClient)
            {
                var blobServiceProvder = provider.GetService<BlobServiceClient>();
                if (blobServiceProvder != null)
                {
                    options.GetBlobContainerClient = () => blobServiceProvder.GetBlobContainerClient(options.ContainerName);
                }
            }

            setupAction?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds Azure Blobs distributed caching services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="setupAction">An <see cref="Action{AzureBlobsCacheOptions, IServiceProvider}"/> to configure the provided <see cref="AzureBlobsCacheOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddAzureBlobCache(this IServiceCollection services, Action<AzureBlobsCacheOptions, IServiceProvider> setupAction)
    {
        Guard.IsNotNull(services);
        Guard.IsNotNull(setupAction);

        return services.AddOptions()
            .AddSingleton<IBufferDistributedCache, AzureBlobsCache>()
            .AddSingleton<IDistributedCache>(provider => provider.GetRequiredService<IBufferDistributedCache>())
            .AddSingleton<IConfigureOptions<AzureBlobsCacheOptions>>(provider =>
                new ConfigureNamedOptions<AzureBlobsCacheOptions, IServiceProvider>(Options.Options.DefaultName, provider, setupAction));
    }
}

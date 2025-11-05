// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for registering output binding services in an <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>This class contains methods to configure and register services required for output binding
/// functionality in worker-based Azure Functions. These services include context accessors, parameter binders, and
/// collectors for handling output bindings.</remarks>
public static class BindingStorageBlobServiceCollectionExtensions
{
    /// <summary>
    /// Adds the necessary services for output binding to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services"> The <see cref="IServiceCollection"/> to add the output binding services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> with the output binding services added.</returns>
    public static IServiceCollection AddAzureBlobsConverters(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddConverter((BlobClient value) => value.GetParentBlobContainerClient().GetPageBlobClient(value.Name));
        services.AddConverter((BlobClient value) => value.GetParentBlobContainerClient().GetAppendBlobClient(value.Name));
        services.AddConverter((BlobClient value) => value.GetParentBlobContainerClient().GetBlobBaseClient(value.Name));
        services.AddConverter((BlobClient value) => value.GetParentBlobContainerClient().GetBlockBlobClient(value.Name));

        return services;
    }
}

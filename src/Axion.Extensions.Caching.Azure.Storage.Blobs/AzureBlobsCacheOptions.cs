// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using Azure.Storage.Blobs;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Options;

namespace Axion.Extensions.Caching.Azure.Storage.Blobs;

/// <summary>
/// Configuration options for <see cref="AzureBlobsCache"/>.
/// </summary>
public class AzureBlobsCacheOptions : IOptions<AzureBlobsCacheOptions>
{
    Func<BlobContainerClient> getBlobContainerClient;

    /// <summary>
    /// Initializes a new instance of <see cref="AzureBlobsCacheOptions"/>.
    /// </summary>
    public AzureBlobsCacheOptions() =>
        getBlobContainerClient = DefaultGetBlobContainerClient;

    /// <summary>
    /// A prefix for blobs created. 
    /// </summary>
    public string? BlobPrefix { get; set; }

    /// <summary>
    /// Indicates if the Azure storage blob container shall be created if missing.
    /// </summary>
    [DefaultValue(true)]
    public bool CreateContainerIfNotExists { get; set; } = true;

    /// <summary>
    /// The periodic interval to scan and delete expired items in the cache. Default is 30 minutes.
    /// </summary>
    public TimeSpan ExpiredItemsDeletionInterval { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// <see langword="true"> if back ground deletion is disabled.</see>
    /// </summary>
    public bool DisableBackgroundExpiredItemsDeletion { get; set; }

    /// <summary>
    /// The connection string to the Azure storage.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Azure storage blob container name.
    /// </summary>
    [DefaultValue("cache-values")]
    public string ContainerName { get; set; } = "cache-values";

    BlobContainerClient DefaultGetBlobContainerClient()
    {
        Guard.IsNotNullOrWhiteSpace(ConnectionString);
        Guard.IsNotNullOrWhiteSpace(ContainerName);

        return new(ConnectionString, ContainerName);
    }

    /// <summary>
    /// A <see cref="BlobContainerClient"/> factory method.
    /// </summary>
    public Func<BlobContainerClient> GetBlobContainerClient
    {
        get => getBlobContainerClient;
        set
        {
            Guard.IsNotNull(value);

            getBlobContainerClient = value;
        }
    }

    AzureBlobsCacheOptions IOptions<AzureBlobsCacheOptions>.Value =>
        this;
}

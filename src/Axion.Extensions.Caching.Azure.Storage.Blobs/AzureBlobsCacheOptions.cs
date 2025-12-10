// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace Axion.Extensions.Caching.Azure.Storage.Blobs;

/// <summary>
/// Configuration options for <see cref="AzureBlobsCache"/>.
/// </summary>
public class AzureBlobsCacheOptions : IOptions<AzureBlobsCacheOptions>
{
    Func<BlobContainerClient>? getBlobContainerClient;

    internal bool UsesDefaultGetBlobContainerClient => getBlobContainerClient == null;

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
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Azure storage blob container name.
    /// </summary>
    [DefaultValue("cache-values")]
    public string ContainerName { get; set; } = "cache-values";

    BlobContainerClient DefaultGetBlobContainerClient()
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(ConnectionString);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(ContainerName);

        return new(ConnectionString, ContainerName);
    }

    /// <summary>
    /// A <see cref="BlobContainerClient"/> factory method.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public Func<BlobContainerClient> GetBlobContainerClient
    {
        get => getBlobContainerClient ?? DefaultGetBlobContainerClient;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            getBlobContainerClient = value;
        }
    }

    AzureBlobsCacheOptions IOptions<AzureBlobsCacheOptions>.Value =>
        this;
}

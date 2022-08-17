// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using Azure.Storage.Blobs;
using Dawn;
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
    /// A prexix for blobs created. 
    /// </summary>

    /* Unmerged change from project 'Axion.Extensions.Caching.Azure.Storage.Blobs (net6.0)'
    Before:
        public string? BlobPrefix { get; set; }

        /// <summary>
    After:
        public string? BlobPrefix { get; set; }

        /// <summary>
    */

    /* Unmerged change from project 'Axion.Extensions.Caching.Azure.Storage.Blobs (net462)'
    Before:
        public string? BlobPrefix { get; set; }

        /// <summary>
    After:
        public string? BlobPrefix { get; set; }

        /// <summary>
    */
    public string? BlobPrefix { get; set; }

    /// <summary>
    /// Indicates if the Azure storage blob container shall be created if missing.
    /// </summary>
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
    public string ContainerName { get; set; } = "cache-values";

    BlobContainerClient DefaultGetBlobContainerClient() =>
        new(Guard.Argument(ConnectionString!).NotNull(), Guard.Argument(ContainerName!).NotNull());

    /// <summary>
    /// A <see cref="BlobContainerClient"/> factory method.
    /// </summary>
    public Func<BlobContainerClient> GetBlobContainerClient
    {
        get => getBlobContainerClient;
        set => getBlobContainerClient = Guard.Argument(value).NotNull().Value;
    }

    AzureBlobsCacheOptions IOptions<AzureBlobsCacheOptions>.Value =>
        this;
}

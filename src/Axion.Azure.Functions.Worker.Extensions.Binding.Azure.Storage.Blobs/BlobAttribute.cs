// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Axion.Azure.Functions.Worker;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Azure.WebJobs;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents an attribute used to bind a parameter or property to an Azure Blob Storage resource.
/// </summary>
/// <remarks>This attribute can be applied to parameters or properties in Azure Functions to bind them to a
/// specific blob or blob container. The binding supports both input and output scenarios, depending on the specified
/// <see cref="FileAccess"/> mode.</remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="BlobAttribute"/> class with the specified blob path.
/// </remarks>
/// <remarks>The <paramref name="blobPath"/> parameter must specify a valid path to a blob. The path can
/// include placeholders for binding expressions.</remarks>
/// <param name="blobPath">The path to the blob in the storage account. This can include the container name and the blob name, separated by
/// a forward slash.</param>
[DebuggerDisplay("{" + nameof(BlobPath) + ",nq}")]
public sealed class BlobAttribute(string blobPath)
    : BindingAttribute([typeof(BlobContainerClient),
        typeof(BlobClient),
        typeof(TextReader),
        typeof(TextWriter),
        typeof(Stream),
        typeof(IAsyncCollector<BinaryData>),
        typeof(AsyncPageable<BlobItem>),
        typeof(IAsyncEnumerable<BlobItem>),
        typeof(IAsyncEnumerable<BlobClient>),
        typeof(IAsyncEnumerable<TextReader>),
        typeof(IAsyncEnumerable<TextWriter>),
        typeof(IAsyncEnumerable<Stream>),
    ])
{

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobAttribute"/> class with the specified blob path and file access
    /// mode.
    /// </summary>
    /// <param name="blobPath">The path to the blob in the storage system. This must be a valid path and cannot be null or empty.</param>
    /// <param name="access">The file access mode that specifies whether the blob is to be read, written.</param>
    public BlobAttribute(string blobPath, FileAccess access) : this(blobPath)
    {
        FileAccess = access;
    }

    /// <summary>
    /// Gets the name of the Azure Blob path to bind to.
    /// </summary>
    public string BlobPath { get; } = blobPath;

    /// <summary>
    /// Gets the file access mode, indicating the level of access permitted for the file.
    /// </summary>
    public FileAccess? FileAccess { get; }

    /// <summary>
    /// Gets or sets the connection string or connection name to the Azure Storage Blob.
    /// </summary>
    public string? Connection { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the blob container should be created if it does not exist.
    /// </summary>
    public bool CreateContainerIfNotExists { get; set; } = true;

    /// <inheritdoc />
    protected override async ValueTask<object?> BindAsync(IServiceProvider serviceProvider, Type type, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(type);

        var nameResolver = serviceProvider.GetService<INameResolver>();
        var (blobPath, connection) = nameResolver == null
            ? (BlobPath, Connection)
            : (nameResolver.ResolveWholeString(BlobPath), nameResolver.ResolveWholeString(Connection));

        string containerName;
        string blobName = null!;
        if (type == typeof(BlobContainerClient))
        {
            containerName = blobPath;
        }
        else
        {
            var slashIndex = blobPath.IndexOf('/');

            if (slashIndex < 0)
            {
                containerName = blobPath;
            }
            else
            {
                containerName = blobPath[..slashIndex];
                blobName = blobPath[(slashIndex + 1)..];
            }
        }

        var azureComponentFactory = serviceProvider.GetAzureComponentFactory();

        var configuration = serviceProvider.GetConfigurationSection(connection);
        var clientOptions = azureComponentFactory.CreateClientOptions<BlobClientOptions>(configuration);

        BlobServiceClient blobServiceClient;

        if (connection?.Contains('=') == true)
        {
            blobServiceClient = new BlobServiceClient(connection, clientOptions);
        }
        else
        {
            if (configuration is IConfigurationSection section && section.Value is string cs)
            {
                blobServiceClient = new BlobServiceClient(cs, clientOptions);
            }
            else
            {
                var blobConfiguration = new BlobConfiguration();

                configuration.Bind(blobConfiguration);

                blobServiceClient = blobConfiguration.BlobServiceUri is null
                    ? (BlobServiceClient)azureComponentFactory.CreateClient(typeof(BlobServiceClient), configuration, null, clientOptions)
                    : new BlobServiceClient(blobConfiguration.BlobServiceUri, clientOptions);
            }
        }

        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        if (CreateContainerIfNotExists)
        {
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }

        if (type == typeof(BlobContainerClient))
        {
            return containerClient;
        }
        else if (type == typeof(IAsyncEnumerable<BlobItem>) || type == typeof(AsyncPageable<BlobItem>) || type == typeof(IAsyncEnumerable<BlobClient>) || type == typeof(IAsyncEnumerable<Stream>))
        {
            var blobs = containerClient.GetBlobsAsync(prefix: blobName, cancellationToken: cancellationToken);

            if (type == typeof(IAsyncEnumerable<BlobItem>) || type == typeof(AsyncPageable<BlobItem>))
            {
                return blobs;
            }

            var blobClients = blobs.Select(b => containerClient.GetBlobClient(b.Name));
            if (type == typeof(IAsyncEnumerable<BlobClient>))
            {
                return blobClients;
            }

            if (type == typeof(IAsyncEnumerable<Stream>) && (FileAccess == null || FileAccess.Value.HasFlag(System.IO.FileAccess.ReadWrite)))
            {
                throw new NotSupportedException($"Binding to {typeof(IAsyncEnumerable<Stream>).FullName} requires specifying {nameof(FileAccess)} as either {nameof(System.IO.FileAccess.Read)} or {nameof(System.IO.FileAccess.Write)}.");
            }

            if (type == typeof(IAsyncEnumerable<TextReader>) || FileAccess == System.IO.FileAccess.Read)
            {
                var streams = blobClients.Select(async (client, token) => (await client.DownloadStreamingAsync(cancellationToken: token).ConfigureAwait(false)).Value.Content);

                return type == typeof(IAsyncEnumerable<Stream>)
                    ? streams
                    : streams.Select(s => (TextReader)new StreamReader(s));
            }
            else
            {
                var streams = blobClients.Select(async (client, token) => await client.GetParentBlobContainerClient().GetAppendBlobClient(client.Name).OpenWriteAsync(false, cancellationToken: token).ConfigureAwait(false));
                return type == typeof(IAsyncEnumerable<Stream>)
                    ? streams
                    : streams.Select(s => (TextWriter)new StreamWriter(s));
            }
        }
        else
        {
            var blobClient = containerClient.GetBlobClient(blobName);
            if (type == typeof(BlobClient))
            {
                return blobClient;
            }

            if (type == typeof(Stream) && (FileAccess == null || FileAccess.Value.HasFlag(System.IO.FileAccess.ReadWrite)))
            {
                throw new NotSupportedException($"Binding to {typeof(Stream).FullName} requires specifying {nameof(FileAccess)} as either {nameof(System.IO.FileAccess.Read)} or {nameof(System.IO.FileAccess.Write)}.");
            }

            if (type == typeof(TextReader) || (type == typeof(Stream) && FileAccess == System.IO.FileAccess.Read))
            {
                var stream = (await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Value.Content;

                return type == typeof(Stream) ? stream : new StreamReader(stream);
            }
            else
            {
                var appendBlobClient = blobClient.GetParentBlobContainerClient().GetAppendBlobClient(blobClient.Name);

                if (type == typeof(IAsyncCollector<BinaryData>))
                {
                    return new BinaryDataAsyncCollector(appendBlobClient);
                }
                else
                {
                    var stream = await appendBlobClient.OpenWriteAsync(false, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (type == typeof(Stream))
                    {
                        return stream;
                    }
                    else
                    {
                        var writer = new StreamWriter(stream);
                        if (type == typeof(TextWriter))
                        {
                            return writer;
                        }
                    }
                }
            }
        }

        return null;
    }

    class BlobConfiguration
    {
        public string? AccountName { get; set; }

        public Uri? BlobServiceUri
        {
            get
            {

                if (field != null)
                {
                    return field;
                }

                if (!string.IsNullOrEmpty(AccountName))
                {
                    return new Uri($"https://{AccountName}.blob.core.windows.net/");
                }

                return null;
            }
            set
            {
                field = value;
            }
        }
    }
}

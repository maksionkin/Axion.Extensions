// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Axion.Extensions.FileProviders;

/// <summary>
/// Provides read-only access to files and directories in a remote Git repository over the Smart HTTP protocol, enabling
/// file enumeration and retrieval through the <see cref="IFileProvider"/> interface.
/// </summary>
/// <remarks>
/// Initializes a new instance of the GitSmartHttpFileProvider class using the specified options.
/// </remarks>
/// <param name="options">The configuration options for the <see cref="GitSmartHttpFileProvider"/>. Cannot be <see langword="null"/>.</param>
/// <param name="httpClient">The HTTP client used for making requests to the Git repository. Can be <see langword="null"/>.</param>
public class GitSmartHttpFileProvider(IOptions<GitFileProviderOptions> options, HttpClient? httpClient = null) : GitSmartFileProvider(options, Uri.UriSchemeHttp, Uri.UriSchemeHttps)
{
    readonly bool ownsHttpClient = httpClient == null;
    readonly HttpClient httpClient = httpClient ?? new();

    /// <summary>
    /// Represents the key used to store or retrieve <see cref="RequestInfo"/> for HTTP requests related to the <see cref="GitSmartHttpFileProvider"/>.
    /// </summary>

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static readonly string HttpRequestOptionsKey = typeof(GitSmartHttpFileProvider).FullName!;

    /// <inheritdoc/>
    protected override async ValueTask<Stream> GetInfoRefsAsync(CancellationToken cancellationToken = default)
    {
        var url = new UriBuilder(Repository)
        {
            Query = "service=git-upload-pack",
        };

        if (!url.Path.EndsWith('/'))
        {
            url.Path += '/';
        }

        url.Path += "info/refs";

        return await GetStreamAsync(url.Uri, HttpMethod.Get, default, cancellationToken);
    }

    /// <inheritdoc/>
    protected override async ValueTask<Stream> GetObjectsAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        var repository = Repository;
        var url = new UriBuilder(repository)
        {
            Path = repository.AbsolutePath.EndsWith('/')
                ? repository.AbsolutePath + "git-upload-pack"
                : repository.AbsolutePath + "/git-upload-pack",
        };

        return await GetStreamAsync(url.Uri, HttpMethod.Post, payload, cancellationToken);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && ownsHttpClient)
        {
            httpClient.Dispose();
        }

        base.Dispose(disposing);
    }

    async ValueTask<Stream> GetStreamAsync(Uri uri, HttpMethod method, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.SetRequestInfo(new(this, payload));

        if (!payload.IsEmpty)
        {
            request.Content = new ReadOnlyMemoryContent(payload);
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        try
        {
            return await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(cancellationToken);
        }
        catch
        {
            response.Dispose();

            throw;
        }
    }

    /// <summary>
    /// Represents information about a Git Smart HTTP request, including the associated file provider and the raw
    /// request data.
    /// </summary>
    /// <param name="Provider">The file provider that handles Git Smart HTTP operations for this request. Cannot be <see langword="null"/>.</param>
    /// <param name="Request">The raw request data as a read-only sequence of bytes.</param>
    public sealed record RequestInfo(GitSmartHttpFileProvider Provider, ReadOnlyMemory<byte> Request);
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Microsoft.Extensions.Options;


namespace Axion.Extensions.FileProviders;

/// <summary>
/// Configuration options for <see cref="GitSmartHttpFileProvider"/>.
/// </summary>
public class GitSmartFileProviderOptions : IOptions<GitSmartFileProviderOptions>
{
    Func<HttpClient> gitHttpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="GitSmartFileProviderOptions"/>.
    /// </summary>
    public GitSmartFileProviderOptions() =>
        gitHttpClient = () => new();

    /// <summary>
    /// Gets or sets the git reference <b>refs/*/*</b> or commit ID.
    /// </summary>
    public string? Reference
    {
        get;
        set;
    }

    /// <summary>
    /// The address of the git repository.
    /// </summary>
    public required Uri Repository { get; set; }


    /// <summary>
    /// A <see cref="HttpClient"/> factory method.
    /// </summary>
    public Func<HttpClient> GetHttpClient
    {
        get => gitHttpClient;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            gitHttpClient = value;
        }
    }

    GitSmartFileProviderOptions IOptions<GitSmartFileProviderOptions>.Value =>
        this;
}

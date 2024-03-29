﻿// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Octokit;

namespace Axion.Extensions.FileProviders;

/// <summary>
/// Configuration options for <see cref="GitHubFileProvider"/>.
/// </summary>
public class GitHubFileProviderOptions : IOptions<GitHubFileProviderOptions>
{
    Func<GitHubClient> getGitHubClient;

    /// <summary>
    /// Initializes a new instance of <see cref="GitHubFileProviderOptions"/>.
    /// </summary>
    public GitHubFileProviderOptions() =>
        getGitHubClient = DefaultGetGitHubClient;

    /// <summary>
    /// The owner of the repository.
    /// </summary>
    public required string Owner { get; init; }

    /// <summary>
    /// The name of the repository.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The name of the commit/branch/tag. Default: the repository’s default branch (usually main).
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// The address to point <see cref="GitHubClient"/> to. Typically used for GitHub Enterprise
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// The <see cref="GitHubClient"/> <see cref="Octokit.Credentials"/>.
    /// </summary>
    public Credentials? Credentials { get; set; }


    GitHubClient DefaultGetGitHubClient()
    {
        var assemblyName = GetType().Assembly.GetName();
        var productHeader = new ProductHeaderValue(assemblyName.Name, assemblyName.Version!.ToString());

        var baseUrl = BaseUrl;

        var githubClient = baseUrl == null
            ? new GitHubClient(productHeader)
            : new GitHubClient(productHeader, baseUrl);

        if (Credentials != null)
        {
            githubClient.Credentials = Credentials;
        }

        return githubClient;
    }

    /// <summary>
    /// A <see cref="GitHubClient"/> factory method.
    /// </summary>
    public Func<GitHubClient> GetGitHubClient
    {
        get => getGitHubClient;
        set
        {
            Guard.IsNotNull(value);

            getGitHubClient = value;
        }
    }

    /// <summary>
    /// Skips reading <see cref="IFileInfo.LastModified"/> property to reduce a numder of GitHub API calls.
    /// </summary>
    public bool SkipLoadingLastModified { get; set; }

    /// <summary>
    /// The base path/tree prefix for accessing only a portion of a repository. 
    /// </summary>
    public string? BasePath { get; set; }
    GitHubFileProviderOptions IOptions<GitHubFileProviderOptions>.Value =>
        this;
}

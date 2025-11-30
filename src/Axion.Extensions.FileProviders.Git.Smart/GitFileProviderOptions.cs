// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;


namespace Axion.Extensions.FileProviders;

/// <summary>
/// Configuration options for <see cref="GitSmartFileProvider"/>.
/// </summary>
public class GitFileProviderOptions : IOptions<GitFileProviderOptions>
{
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
    [Required]
    public required Uri Repository { get; set; }

    GitFileProviderOptions IOptions<GitFileProviderOptions>.Value => this;
}

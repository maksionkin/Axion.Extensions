// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Axion.Extensions.FileProviders;

/// <summary>
/// Provides extension methods for configuring branch and tag references on a <see cref="GitSmartFileProviderOptions"/> instance.
/// </summary>
public static class GitSmartFileProviderOptionsExtensions
{
    extension(GitSmartFileProviderOptions options)
    {
        /// <summary>
        /// Configures the file provider to use the specified Git branch as the reference for file operations.
        /// </summary>
        /// <param name="branch">The name of the Git branch to use. Cannot be null or empty.</param>
        /// <returns>The current <see cref="GitSmartFileProviderOptions"/> instance with the branch reference set.</returns>
        public GitSmartFileProviderOptions UseBranch(string branch)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentException.ThrowIfNullOrEmpty(branch);

            options.Reference = "refs/head/" + branch;

            return options;
        }

        /// <summary>
        /// Configures the provider to use the specified Git tag as the reference for file operations.
        /// </summary>
        /// <param name="tag">The name of the Git tag to use as the reference. Cannot be null or empty.</param>
        /// <returns>The current <see cref="GitSmartFileProviderOptions"/> instance with the reference set to the specified tag.</returns>
        public GitSmartFileProviderOptions UseTag(string tag)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentException.ThrowIfNullOrEmpty(tag);

            options.Reference = "refs/tags/" + tag;

            return options;
        }
    }
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Octokit;

namespace Axion.Extensions.FileProviders;

/// <summary>
/// Looks up files using the GitHub repository.
/// </summary>
public class GitHubFileProvider : IFileProvider
{
    readonly IOptions<GitHubFileProviderOptions> optionsAccessor;

    /// <summary>
    /// Initializes a new instance of <see cref="GitHubFileProvider"/>.
    /// </summary>
    /// <param name="optionsAccessor">The configuration options.</param>
    public GitHubFileProvider(IOptions<GitHubFileProviderOptions> optionsAccessor)
    {
        ArgumentNullException.ThrowIfNull(optionsAccessor);

        this.optionsAccessor = optionsAccessor;
    }

    /// <inheritdoc/>
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var contents = GetContentsAsync(subpath).Result;

        return contents == null || !contents.Value.IsDirectory
            ? NotFoundDirectoryContents.Singleton
            : new GitHubDirectoryContents(contents.Value.Properties, contents.Value.Contents);
    }

    async Task<(IReadOnlyList<RepositoryContent> Contents, bool IsDirectory, string Path, GitHubProperties Properties)?> GetContentsAsync(string subpath)
    {
        var path = Combine(subpath);
        var properties = new GitHubProperties(optionsAccessor);

        var contents = await properties.GetContentsAsync(path);

        return contents == null
            ? default
            : (contents.Value.Contents, contents.Value.IsDirectory, path, properties);
    }

    /// <inheritdoc/>
    public IFileInfo GetFileInfo(string subpath)
    {
        var contents = GetContentsAsync(subpath).Result;

        if (contents == null)
        {
            return new NotFoundFileInfo(subpath);
        }
        else if (contents.Value.IsDirectory)
        {
            var properties = contents.Value.Properties;

            if (string.IsNullOrEmpty(contents.Value.Path))
            {
                return new GitHubFileInfo(contents!.Value.Properties, new("", "", null, -1, ContentType.Dir, null, null, null, contents.Value.Contents[0].HtmlUrl.GetParent(), null, null, null, null));
            }
            else
            {
                var content = properties.GetContentsAsync(contents.Value.Path.GetParent()).Result!.Value.Contents.First(content => content.Path == contents.Value.Path);

                return new GitHubFileInfo(contents!.Value.Properties, content);
            }
        }
        else
        {
            return new GitHubFileInfo(contents.Value.Properties, contents.Value.Contents[0]);
        }
    }

    /// <inheritdoc/>
    public IChangeToken Watch(string filter) =>
        NullChangeToken.Singleton;

    string Combine(string subpath)
    {
        var basePath = optionsAccessor.Value.BasePath.NormalizePathSeparators();
        subpath = subpath.NormalizePathSeparators();

        return string.IsNullOrEmpty(basePath)
            ? subpath
            : basePath + '/' + subpath;
    }

    readonly record struct GitHubProperties(GitHubClient Client, string Owner, string Repo, string? Reference, bool SkipLoadingLastModified)
    {
        public GitHubProperties(GitHubFileProviderOptions options)
            : this(options.GetGitHubClient(),
                  options.Owner,
                  options.Name,
                  options.Reference,
                  options.SkipLoadingLastModified)
        { }

        public GitHubProperties(IOptions<GitHubFileProviderOptions> options)
             : this(options.Value)
        { }

        public IRepositoriesClient RepositoriesClient
        {
            get => Client.Repository;
        }

        public async Task<(IReadOnlyList<RepositoryContent> Contents, bool IsDirectory)?> GetContentsAsync(string path)
        {
            try
            {
                var contentClient = RepositoriesClient.Content;

                var contents = (await (string.IsNullOrEmpty(path)
                    ? (string.IsNullOrEmpty(Reference)
                        ? contentClient.GetAllContents(Owner, Repo)
                        : contentClient.GetAllContentsByRef(Owner, Repo, Reference))
                    : (string.IsNullOrEmpty(Reference)
                        ? contentClient.GetAllContents(Owner, Repo, path)
                        : contentClient.GetAllContentsByRef(Owner, Repo, path, Reference))))
                    .Where(content => content.Type == ContentType.File || content.Type == ContentType.Dir) // TODO: support submodule and symlink
                    .ToList();

                if (contents != null && contents.Count > 0)
                {
                    return (contents, contents.Count != 1 || contents[0].Path != path);
                }
            }
            catch (NotFoundException)
            {
            }

            return default;
        }
    }

    class GitHubFileInfo : IFileInfo
    {
        readonly string? sha;
        readonly GitHubProperties properties;
        readonly RepositoryContent content;

        public GitHubFileInfo(GitHubProperties properties, RepositoryContent content)
        {
            this.properties = properties;
            this.content = content;

            if (properties.SkipLoadingLastModified)
            {
                sha = properties.Reference;

                LastModified = DateTimeOffset.UtcNow;

                Exists = true;
            }
            else
            {
                try
                {
                    var commit = properties.RepositoriesClient.Commit.GetAll(
                            properties.Owner,
                            properties.Repo,
                            new()
                            {
                                Path = content.Path,
                                Sha = properties.Reference
                            },
                            new()
                            {
                                PageSize = 1
                            })
                        .Result[0];

                    LastModified = commit.Commit.Committer.Date;

                    sha = commit.Sha;

                    Exists = true;
                }
                catch (NotFoundException)
                {
                    Exists = false;
                }
            }
        }

        public bool Exists { get; }

        public long Length
        {
            get => IsDirectory || !Exists ? -1 : content.Size;
        }

        public string? PhysicalPath =>
            null;

        public string Name
        {
            get => content.Name;
        }

        public DateTimeOffset LastModified { get; }

        public bool IsDirectory
        {
            get => content.Type.Value == ContentType.Dir;
        }

        public Stream CreateReadStream()
        {
            if (IsDirectory || !Exists)
            {
                throw new InvalidOperationException();
            }

            var data = content.EncodedContent != null && content.Encoding == "base64"
                ? Convert.FromBase64String(content.EncodedContent)
                : (string.IsNullOrEmpty(sha)
                    ? properties.RepositoriesClient.Content.GetRawContent(properties.Owner, properties.Repo, content.Path)
                    : properties.RepositoriesClient.Content.GetRawContentByRef(properties.Owner, properties.Repo, content.Path, sha))
                    .Result;

            return new MemoryStream(data);
        }

        internal string HtmlUrl
        {
            get => content.HtmlUrl;
        }

        public override string ToString() =>
           HtmlUrl;
    }

    class GitHubDirectoryContents(GitHubFileProvider.GitHubProperties properties, IEnumerable<RepositoryContent> contents) : IDirectoryContents
    {
        readonly IEnumerable<GitHubFileInfo> fileInfos = [.. contents.Select(content => new GitHubFileInfo(properties, content))];

        public bool Exists =>
            true;

        public IEnumerator<IFileInfo> GetEnumerator() =>
            fileInfos.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        public override string ToString() =>
            fileInfos.FirstOrDefault(file => file.IsDirectory)?.HtmlUrl ?? fileInfos.First().HtmlUrl.ReplaceFirst("/blob/", "/tree/")
                .GetParent();
    }
}

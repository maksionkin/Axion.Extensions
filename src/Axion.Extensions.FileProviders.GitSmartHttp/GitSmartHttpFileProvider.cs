// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Axion.Extensions.FileProviders;

/// <summary>
/// Provides read-only access to files and directories in a remote Git repository over the Smart HTTP protocol, enabling
/// file enumeration and retrieval through the <see cref="IFileProvider"/> interface.
/// </summary>
public class GitSmartHttpFileProvider : IFileProvider
{
    /// <summary>
    /// Represents the key used to store or retrieve <see cref="RequestInfo"/> for HTTP requests related to the <see cref="GitSmartHttpFileProvider"/>.
    /// </summary>

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static readonly string HttpRequestOptionsKey = typeof(GitSmartHttpFileProvider).FullName!;

    static readonly Encoding Utf8 = new UTF8Encoding(false, false);

    static readonly Dictionary<ObjectType, byte[]> Prefixes = new()
    {
        [ObjectType.Commit] = Utf8.GetBytes("commit "),
        [ObjectType.Tree] = Utf8.GetBytes("tree "),
        [ObjectType.Blob] = Utf8.GetBytes("blob "),
    };

    readonly GitSmartFileProviderOptions options;
    readonly Uri uploadPackUri;
    readonly Dictionary<string, (string Oid, bool Folder)> objects;
    readonly DateTimeOffset lastModified;
    readonly ConcurrentDictionary<string, long> blobLengths = new();
    readonly ConcurrentDictionary<string, Lazy<object>> getLengthLocks = new();


    /// <summary>
    /// Initializes a new instance of the GitSmartHttpFileProvider class using the specified options.
    /// </summary>
    /// <param name="options">The configuration options for the <see cref="GitSmartHttpFileProvider"/>. Cannot be <see langword="null"/>.</param>
    public GitSmartHttpFileProvider(IOptions<GitSmartFileProviderOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNotEqual(options.Value.Repository.IsAbsoluteUri, true);
        ArgumentOutOfRangeException.ThrowIfNotEqual(options.Value.Repository.Scheme == Uri.UriSchemeHttp || options.Value.Repository.Scheme == Uri.UriSchemeHttps, true);

        this.options = options.Value;
        uploadPackUri = new UriBuilder(this.options.Repository)
        {
            Path = this.options.Repository.AbsolutePath.EndsWith('/')
                ? this.options.Repository.AbsolutePath + "git-upload-pack"
                : this.options.Repository.AbsolutePath + "/git-upload-pack",
        }.Uri;

        async Task<(Dictionary<string, (string Oid, bool Folder)>, DateTimeOffset)> InitialObjectPopulateAsync()
        {
            var info = await GetRepoInfoAsync(this.options).ConfigureAwait(false);

            return await PopulateObjectsAsync(info.ServerCapabilities, info.Oid).ConfigureAwait(false);
        }

        (objects, lastModified) = Task.Run(InitialObjectPopulateAsync).ConfigureAwait(false).GetAwaiter().GetResult();

        if (lastModified == default)
        {
            lastModified = DateTimeOffset.UtcNow;
        }
    }

    enum ObjectFormat
    {
        Sha1,
        Sha256,
    }

    enum ObjectType
    {
        Commit = 1 << 4,
        Tree = 2 << 4,
        Blob = 3 << 4,
        Tag = 4 << 4,
        OfsDelta = 6 << 4,
        RefDelta = 7 << 4,
    }

    enum TreeObjectType
    {
        File = 0b1_000_000_000_000_000, // 100ooo
        Directory = 0b100_000_000_000_000, // 40000
    }

    /// <inheritdoc/>
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        subpath = GetNormilizedPath(subpath);

        if (!objects.TryGetValue(subpath, out var i) || !i.Folder)
        {
            return NotFoundDirectoryContents.Singleton;
        }

        subpath += '/';

        GitSmartHttpFileInfo? GetGitSmartHttpFileInfo(KeyValuePair<string, (string Oid, bool Folder)> p)
        {
            if (p.Key.StartsWith(subpath, StringComparison.Ordinal))
            {
                var relativePath = p.Key[subpath.Length..];

                if (relativePath.IndexOf('/') < 0)
                {
                    return new(this, relativePath, p.Value.Oid, p.Value.Folder);
                }
            }

            return null;
        }

        return new GitSmartHttpDirectoryContents(objects.Select(GetGitSmartHttpFileInfo).Where(f => f != null).ToList()!);
    }

    /// <inheritdoc/>
    public IFileInfo GetFileInfo(string subpath)
    {
        subpath = GetNormilizedPath(subpath);

        return objects.TryGetValue(subpath, out var i)
            ? new GitSmartHttpFileInfo(this, subpath.Split('/').Last(), i.Oid, i.Folder)
            : new NotFoundFileInfo(subpath);
    }

    /// <inheritdoc/>
    public IChangeToken Watch(string filter) =>
        NullChangeToken.Singleton;

    static string GetNormilizedPath(string subpath, [CallerArgumentExpression(nameof(subpath))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(subpath, paramName);

        if (subpath == "/" || subpath == "")
        {
            subpath = "";
        }
        else if (!subpath.StartsWith('/'))
        {
            subpath = '/' + subpath;
        }

        if (subpath.EndsWith('/'))
        {
            subpath = subpath[..^1];
        }

        return subpath.Replace('\\', '/');
    }

    static bool IsHex(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');


    static async Task<(ServerCapabilities ServerCapabilities, string Oid)> GetRepoInfoAsync(GitSmartFileProviderOptions options, CancellationToken cancellationToken = default)
    {
        ServerCapabilities serverCapabilities = default;

        var url = new UriBuilder(options.Repository)
        {
            Query = "service=git-upload-pack",
        };

        if (!url.Path.EndsWith('/'))
        {
            url.Path += '/';
        }

        url.Path += "info/refs";

        using var request = new HttpRequestMessage(HttpMethod.Get, url.Uri);

        using var response = await options.GetHttpClient().SendAsync(request, cancellationToken);

        using var stream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(cancellationToken);
        using var pktStream = new PktLineReadStream(stream, false);
        using var reader = new StreamReader(pktStream, Utf8, false, -1, true);

        var first = true;
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
            {
                break;
            }

            if (line.StartsWith('#') || line.Length == 0)
            {
                continue;
            }

            var split = line.Split(' ', 2);
            if (split.Length != 2)
            {
                throw new FormatException("Unxpected payload.");
            }

            var oid = split[0];
            var refs = split[1];
            if (first)
            {
                first = false;

                split = refs.Split('\0', 2);
                refs = split[0];

                if (split.Length == 2)
                {
                    var objectFormat = ObjectFormat.Sha1;
                    var filter = false;

                    var capabilities = split[1].Split(' ');
                    foreach (var capability in capabilities)
                    {
                        if (capability.StartsWith("object-format=", StringComparison.Ordinal))
                        {
                            var format = capability["object-format=".Length..];
                            if (format == "sha1")
                            {
                                objectFormat = ObjectFormat.Sha1;
                            }
                            else if (format == "sha256")
                            {
                                objectFormat = ObjectFormat.Sha256;
                            }
                            else
                            {
                                throw new FormatException($"Unknown 'object-format' '{format}'.");
                            }
                        }
                        else if (capability == "filter")
                        {
                            filter = true;
                        }
                    }

                    serverCapabilities = new(objectFormat, filter);
                }

                if (options.Reference?.Length == serverCapabilities.IdStringSize && options.Reference.All(IsHex))
                {
                    return (serverCapabilities, oid);
                }
            }

            if (string.IsNullOrEmpty(options.Reference) || options.Reference == refs)
            {
                return (serverCapabilities, oid);
            }
        }

        throw new FormatException($"Cannot resolve '{options.Reference}'");
    }

    async Task<(Dictionary<string, (string Oid, bool Folder)> Objects, DateTimeOffset LastModified)> PopulateObjectsAsync(ServerCapabilities capabilities, string oid, CancellationToken cancellationToken = default)
    {
        var res = new Dictionary<string, (string Oid, bool Folder)>();
        DateTimeOffset commitDate = default;

        using var ms = new MemoryStream(256);
        ms.WritePrkLine("want " + oid + (capabilities.Filter ? " filter" : null));
        ms.WritePrkLine("deepen 1");

        if (capabilities.Filter)
        {
            ms.WritePrkLine("filter blob:none");
        }

        ms.FlushPktLine();
        ms.WritePrkLine("done");

        ms.Position = 0;

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadPackUri);
        request.SetRequestInfo(new(this, oid, []));
        request.Content = new StreamContent(ms);

        using var response = await options.GetHttpClient().SendAsync(request, cancellationToken);
        using var stream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(cancellationToken);
        using (var pktStream = new PktLineReadStream(stream, true))
        {
            await pktStream.SkipToEndAsync(cancellationToken);
        }

        var packHeader = new byte[16];
        await stream.ReadAtLeastAsync(packHeader.AsMemory(0, 8), 8, cancellationToken: cancellationToken);

        // var version = BinaryPrimitives.ReadUInt32BigEndian(packHeader.AsSpan(0, 4));

        long objectCount = BinaryPrimitives.ReadUInt32BigEndian(packHeader.AsSpan(4, 4));

        var neededTrees = new Dictionary<string, string>();
        var availableTrees = new Dictionary<string, List<TreeEntry>>();
        for (long i = 0; i < objectCount; i++)
        {
            await stream.ReadAtLeastAsync(packHeader.AsMemory(0, 1), 1, cancellationToken: cancellationToken);

            var objectType = (ObjectType)(packHeader[0] & 0x70);

            long objectSize = packHeader[0] & 0xF;
            var shift = 4;

            while ((packHeader[0] & 0x80) != 0)
            {
                await stream.ReadAtLeastAsync(packHeader.AsMemory(0, 1), 1, cancellationToken: cancellationToken);

                objectSize |= (long)(packHeader[0] & 0x7F) << shift;
                shift += 7;
            }

            if (objectSize > 0)
            {
                using HashAlgorithm hash = capabilities.CreateHashAlgorithm();

                var prefix = Prefixes[objectType];
                hash.TransformBlock(prefix, 0, prefix.Length, null, 0);

                Utf8Formatter.TryFormat(objectSize, packHeader, out var written);
                packHeader[written] = 0;
                hash.TransformBlock(packHeader, 0, written + 1, null, 0);

                using var zlib = new ZLibFixedStream(stream, objectSize, hash, []);

                List<TreeEntry>? treeEntries = null;
                string? treeObjectId = null;
                DateTimeOffset last = default;
                switch (objectType)
                {
                    case ObjectType.Commit:
                        (treeObjectId, last) = await GetTreeForCommitAsync(zlib, cancellationToken);
                        break;

                    case ObjectType.Tree:
                        treeEntries = await ParseTreeCommitAsync(zlib, capabilities, cancellationToken);
                        break;
                }

                await zlib.SkipToEndAsync(cancellationToken);

                if (objectType == ObjectType.Blob)
                {
                    var objectId = Convert.ToHexStringLower(hash.Hash!);
                    blobLengths.GetOrAdd(objectId, objectSize);
                }

                if (treeObjectId != null || treeEntries != null)
                {
                    var objectId = Convert.ToHexStringLower(hash.Hash!);

                    List<TreeEntry>? entries = null;
                    string? prefixPath = null;
                    if (treeObjectId != null && objectId == oid)
                    {
                        if (commitDate < last)
                        {
                            commitDate = last;
                        }

                        res[""] = new(treeObjectId, true);

                        if (availableTrees.TryGetValue(treeObjectId, out entries))
                        {
                            availableTrees.Remove(treeObjectId);
                            prefixPath = "/";
                        }
                        else
                        {
                            neededTrees.Add(treeObjectId, "/");
                        }
                    }
                    else if (treeEntries != null)
                    {
                        if (neededTrees.TryGetValue(objectId, out prefixPath))
                        {
                            neededTrees.Remove(objectId);
                            entries = treeEntries;
                        }
                        else
                        {
                            availableTrees.Add(objectId, treeEntries);
                        }
                    }

                    if (entries != null)
                    {
                        void ProcessTree(string path, List<TreeEntry> entries)
                        {
                            foreach (var entry in entries)
                            {
                                var name = path + entry.Name;
                                bool? folder = null;

                                switch (entry.Type)
                                {
                                    case TreeObjectType.Directory:
                                        folder = true;
                                        var p = name + '/';
                                        if (availableTrees.TryGetValue(entry.Id, out var e))
                                        {
                                            availableTrees.Remove(entry.Id);

                                            ProcessTree(p, e);
                                        }
                                        else
                                        {
                                            neededTrees.Add(entry.Id, p);
                                        }
                                        break;

                                    case TreeObjectType.File:
                                        folder = false;
                                        break;
                                }

                                if (folder != null)
                                {
                                    res[name] = new(entry.Id, folder.Value);
                                }
                            }
                        }

                        ProcessTree(prefixPath!, entries);
                    }
                }
            }
        }

        return (res, commitDate);
    }

    static async ValueTask<(string Oid, DateTimeOffset LastModified)> GetTreeForCommitAsync(Stream stream, CancellationToken cancellationToken)
    {
        string? oid = null;
        DateTimeOffset date = default;
        using var sr = new StreamReader(stream, Utf8, false, -1, true);
        while (true)
        {
            var line = await sr.ReadLineAsync(cancellationToken);
            if (line == null)
            {
                break;
            }

            if (line.StartsWith("tree ", StringComparison.Ordinal))
            {
                oid = line["tree ".Length..];
            }
            else if (line.StartsWith("committer ", StringComparison.Ordinal) || line.StartsWith("author ", StringComparison.Ordinal))
            {
                var split = line.Split(' ');

                if (split.Length > 3
                    && split[^1].Length <= 5 && int.TryParse(split.Last(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var tz) && (Math.Abs(tz) % 100) < 60
                    && long.TryParse(split[^2], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
                {
                    var abs = Math.Abs(tz);
                    var offset = new TimeSpan(abs / 100, abs % 100, 0);
                    if (tz < 0)
                    {
                        offset = -offset;
                    }

                    var d = DateTimeOffset.FromUnixTimeSeconds(seconds).ToOffset(offset);
                    if (d > date)
                    {
                        date = d;
                    }
                }
            }
        }

        if (oid == null)
        {
            throw new FormatException("Invalid payload.");
        }

        return new(oid, date);
    }

    static async ValueTask<List<TreeEntry>> ParseTreeCommitAsync(Stream stream, ServerCapabilities capabilities, CancellationToken cancellationToken)
    {
        var res = new List<TreeEntry>();
        var objectIdLength = capabilities.IdStringSize / 2;

        var buffer = new byte[objectIdLength];

        while (true)
        {
            var mode = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
                if (read == 0)
                {
                    return res;
                }

                if (buffer[0] == ' ')
                {
                    break;
                }
                else if ('0' <= buffer[0] && buffer[0] <= '7')
                {
                    mode <<= 3;
                    mode += (buffer[0] - '0');
                }
                else
                {
                    throw new FormatException("Invalid payload.");
                }
            }

            using var nameStream = new ReadToZeroStream(stream);
            var type = (TreeObjectType)(mode & 0xFE00);
            string? name = null;
            switch (type)
            {
                case TreeObjectType.File:
                case TreeObjectType.Directory:
                    using (var reader = new StreamReader(nameStream, Utf8, false, -1, true))
                    {
                        name = await reader.ReadToEndAsync(cancellationToken);
                    }

                    break;

                default:
                    await nameStream.SkipToEndAsync(cancellationToken);
                    break;
            }

            await stream.ReadAtLeastAsync(buffer.AsMemory(0, objectIdLength), objectIdLength, true, cancellationToken);

            if (name != null)
            {
                res.Add(new(type, name, Convert.ToHexStringLower(buffer, 0, objectIdLength)));
            }
        }
    }

    async Task<Stream> LoadStreamAsync(string oid, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream(256);
        ms.WritePrkLine("want " + oid);

        ms.FlushPktLine();
        ms.WritePrkLine("done");

        ms.Position = 0;

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadPackUri);
        request.SetRequestInfo(new(this, oid, []));
        request.Content = new StreamContent(ms);

        var response = await options.GetHttpClient().SendAsync(request, cancellationToken);
        try
        {
            var stream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(cancellationToken);

            try
            {
                using (var pktStream = new PktLineReadStream(stream, true))
                {
                    await pktStream.SkipToEndAsync(cancellationToken);
                }

                var packHeader = new byte[16];
                await stream.ReadAtLeastAsync(packHeader.AsMemory(0, 8), 8, cancellationToken: cancellationToken);

                // var version = BinaryPrimitives.ReadUInt32BigEndian(packHeader.AsSpan(0, 4));

                long objectCount = BinaryPrimitives.ReadUInt32BigEndian(packHeader.AsSpan(4, 4));

                for (long i = 0; i < objectCount; i++)
                {
                    await stream.ReadAtLeastAsync(packHeader.AsMemory(0, 1), 1, cancellationToken: cancellationToken);

                    var objectType = (ObjectType)(packHeader[0] & 0x70);

                    long objectSize = packHeader[0] & 0xF;
                    var shift = 4;

                    while ((packHeader[0] & 0x80) != 0)
                    {
                        await stream.ReadAtLeastAsync(packHeader.AsMemory(0, 1), 1, cancellationToken: cancellationToken);

                        objectSize |= (long)(packHeader[0] & 0x7F) << shift;
                        shift += 7;
                    }

                    if (objectType == ObjectType.Blob)
                    {
                        blobLengths.GetOrAdd(oid, objectSize);
                        getLengthLocks.TryRemove(oid, out _);

                        Stream res = objectSize > 0 ? new ZLibFixedStream(stream, objectSize, null, [response, stream]) : new MemoryStream([]);
                        stream = null;
                        response = null;

                        return res;
                    }

                    if (objectSize > 0)
                    {
                        using var zlib = new ZLibFixedStream(stream, objectSize, null, []);
                        await zlib.SkipToEndAsync(cancellationToken);
                    }
                }
            }
            finally
            {
                stream?.Dispose();
            }
        }
        finally
        {
            response?.Dispose();
        }

        throw new FormatException("Invalid payload.");
    }

    /// <summary>
    /// Represents information about a Git Smart HTTP request, including the target repository, the requested object,
    /// and the set of objects already held by the client.
    /// </summary>
    /// <remarks>This class is intended for advanced scenarios involving low-level interactions with Git
    /// repositories over the Smart HTTP protocol. Instances of this class are immutable and can be compared for
    /// equality based on their repository, requested object, and held object IDs.</remarks>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public sealed class RequestInfo : IEquatable<RequestInfo>
    {
        internal RequestInfo(GitSmartHttpFileProvider provider, string want, IEnumerable<string> have)
        {
            Provider = provider;
            Want = want;
            Have = have.OrderBy(i => i);
        }

        /// <summary>
        /// Gets the file provider used to access Git repositories over Smart HTTP.
        /// </summary>
        public GitSmartHttpFileProvider Provider { get; }

        /// <summary>
        /// Gets the requested object id.
        /// </summary>
        public string Want { get; }

        /// <summary>
        /// Gets the collection of object ids currently held by the instance.
        /// </summary>
        public IEnumerable<string> Have { get; }

        /// <summary>
        /// Gets the URI of the repository associated.
        /// </summary>
        public Uri Repository => Provider.options.Repository;

        /// <inheritdoc/>
        public override bool Equals(object? obj) =>
            Equals(obj as RequestInfo);

        /// <inheritdoc/>
        public bool Equals(RequestInfo? other) =>
            other != null
                && Repository == other.Repository
                && Want == other.Want
                && Have.SequenceEqual(other.Have);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Repository);
            hash.Add(Want);

            foreach (var i in Have)
            {
                hash.Add(i);
            }

            return hash.ToHashCode();
        }
    }

    record struct TreeEntry(TreeObjectType Type, string Name, string Id);

    record struct ServerCapabilities(ObjectFormat ObjectFormat, bool Filter)
    {
        public readonly int IdStringSize =>
            ObjectFormat == ObjectFormat.Sha1 ? 40 : 64;

        public readonly HashAlgorithm CreateHashAlgorithm() =>
            ObjectFormat == ObjectFormat.Sha1 ? SHA1.Create() : SHA256.Create();
    }

    class GitSmartHttpDirectoryContents(IEnumerable<IFileInfo> entries) : IDirectoryContents
    {
        public bool Exists => true;
        public IEnumerator<IFileInfo> GetEnumerator() => entries.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    class GitSmartHttpFileInfo(GitSmartHttpFileProvider provider, string name, string oid, bool folder) : IFileInfo
    {
        public bool Exists => true;

        public long Length
        {
            get
            {
                long? GetLength() =>
                    IsDirectory
                        ? -1
                        : (provider.blobLengths.TryGetValue(oid, out var l) ? l : null);

                if (GetLength() == null)
                {
                    lock (provider.getLengthLocks.GetOrAdd(oid, value: new()).Value)
                    {
                        if (GetLength() == null)
                        {
                            using var stream = Task.Run(async () => await provider.LoadStreamAsync(oid).ConfigureAwait(false))
                                .ConfigureAwait(false)
                                .GetAwaiter()
                                .GetResult();
                        }
                    }

                    provider.getLengthLocks.TryRemove(oid, out _);
                }

                return GetLength()!.Value;
            }
        }

        public string? PhysicalPath => null;

        public string Name => name;

        public DateTimeOffset LastModified => provider.lastModified;

        public bool IsDirectory => folder;

        public Stream CreateReadStream() =>
            !IsDirectory
                ? provider.LoadStreamAsync(oid).ConfigureAwait(false).GetAwaiter().GetResult()
                : throw new FileNotFoundException();
    }
}

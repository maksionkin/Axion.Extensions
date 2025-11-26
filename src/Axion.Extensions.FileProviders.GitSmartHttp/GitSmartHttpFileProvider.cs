// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
    readonly Task<(ServerCapabilities ServerCapabilities, string Oid, bool Explicit)> repoInfo;
    readonly Lazy<Task> initialObjectPopulate;
    readonly ConcurrentDictionary<string, (string Oid, bool Folder)> objects = new();
    readonly ConcurrentDictionary<string, long> blobLengths = new();

    /// <summary>
    /// Initializes a new instance of the GitSmartHttpFileProvider class using the specified options.
    /// </summary>
    /// <param name="options">The configuration options for the <see cref="GitSmartHttpFileProvider"/>. Cannot be <see langword="null"/>.</param>
    public GitSmartHttpFileProvider(IOptions<GitSmartFileProviderOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options.Value;

        repoInfo = Task.Run(async () => await GetRepoInfoAsync().ConfigureAwait(false));

        async ValueTask InitialObjectPopulateAsync()
        {
            var info = await repoInfo.ConfigureAwait(false);
            if (info != default)
            {
                await PopulateObjectsAsync().ConfigureAwait(false);
            }
        }

        initialObjectPopulate = new(() => Task.Run(async () => await InitialObjectPopulateAsync().ConfigureAwait(false)));
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
        ArgumentNullException.ThrowIfNull(subpath);

        var s = subpath.StartsWith('/') ? subpath : '/' + subpath;

        EnsureInitialPopulate();


        if (!objects.TryGetValue(s, out var i) || !i.Folder)
        {
            return NotFoundDirectoryContents.Singleton;
        }

        if (!s.EndsWith('/'))
        {
            s += '/';
        }

        return new GitSmartHttpDirectoryContents(objects.Where(p => p.Key.StartsWith(s, StringComparison.Ordinal) && p.Key.LastIndexOf('/') == s.Length - 1)
            .Select(p => new GitSmartHttpFileInfo(this, p.Key, p.Value.Oid, p.Value.Folder))
            .ToList());
    }

    /// <inheritdoc/>
    public IFileInfo GetFileInfo(string subpath)
    {
        ArgumentNullException.ThrowIfNull(subpath);

        var s = subpath.StartsWith('/') ? subpath : '/' + subpath;

        if (objects.TryGetValue(s, out var i))
        {
            return new GitSmartHttpFileInfo(this, subpath, i.Oid, i.Folder);
        }
        else
        {
            return new NotFoundFileInfo(subpath);
        }
    }

    /// <inheritdoc/>
    public IChangeToken Watch(string filter) =>
        NullChangeToken.Singleton;

    static bool IsHex(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');

    void EnsureInitialPopulate() =>
        initialObjectPopulate.Value.ConfigureAwait(false).GetAwaiter().GetResult();

    async Task<(ServerCapabilities ServerCapabilities, string Oid, bool Explicit)> GetRepoInfoAsync(CancellationToken cancellationToken = default)
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
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return default;
        }

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
                    var shallow = false;

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
                        else if (capability == "shallow")
                        {
                            shallow = true;
                        }
                    }

                    serverCapabilities = new(objectFormat, filter, shallow);
                }

                if (options.Reference?.Length == serverCapabilities.IdStringSize && options.Reference.All(IsHex))
                {
                    return (serverCapabilities, oid, true);
                }
            }

            if (string.IsNullOrEmpty(options.Reference) || options.Reference == refs)
            {
                return (serverCapabilities, oid, false);
            }
        }

        throw new FormatException($"Cannot resolve '{options.Reference}'");
    }

    Uri GetUploadPackServiceUri()
    {
        var url = new UriBuilder(options.Repository);
        if (!url.Path.EndsWith('/'))
        {
            url.Path += '/';
        }

        url.Path += "git-upload-pack";

        return url.Uri;
    }

    async Task PopulateObjectsAsync(CancellationToken cancellationToken = default)
    {
        var (capabilities, oid, _) = await repoInfo.ConfigureAwait(false);

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

        using var request = new HttpRequestMessage(HttpMethod.Post, GetUploadPackServiceUri());
        request.SetRequestInfo(new(this, oid));

        using var response = await options.GetHttpClient().PostAsync(GetUploadPackServiceUri(), new StreamContent(ms), cancellationToken);
        using var stream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(cancellationToken);
        using (var pktStream = new PktLineReadStream(stream, true))
        {
            await pktStream.SkipToEndAsync(cancellationToken);
        }

        var packHeader = new byte[16];
        await stream.ReadAtLeastAsync(packHeader.AsMemory(0, 8), 8, cancellationToken: cancellationToken);

        var version = BinaryPrimitives.ReadUInt32BigEndian(packHeader.AsSpan(0, 4));

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

                using var zlib = new ZLibFixedStream(stream, objectSize, hash);

                List<TreeEntry>? treeEntries = null;
                string? treeObjectId = null;
                switch (objectType)
                {
                    case ObjectType.Commit:
                        treeObjectId = await GetTreeForCommitAsync(zlib, cancellationToken);
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
                        objects.GetOrAdd("/", value: new(treeObjectId, true));

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
                                    objects.GetOrAdd(name, value: new(entry.Id, folder.Value));
                                }
                            }
                        }

                        ProcessTree(prefixPath!, entries);
                    }
                }
            }
        }
    }

    async ValueTask<string> GetTreeForCommitAsync(Stream stream, CancellationToken cancellationToken)
    {
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
                return line["tree ".Length..];
            }
        }

        return null!;
    }

    async ValueTask<List<TreeEntry>> ParseTreeCommitAsync(Stream stream, ServerCapabilities capabilities, CancellationToken cancellationToken)
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

        using var request = new HttpRequestMessage(HttpMethod.Post, GetUploadPackServiceUri());
        request.SetRequestInfo(new(this, oid));

        using var response = await options.GetHttpClient().PostAsync(GetUploadPackServiceUri(), new StreamContent(ms), cancellationToken);
        using var stream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync(cancellationToken);
        using (var pktStream = new PktLineReadStream(stream, true))
        {
            await pktStream.SkipToEndAsync(cancellationToken);
        }

        var packHeader = new byte[16];
        await stream.ReadAtLeastAsync(packHeader.AsMemory(0, 8), 8, cancellationToken: cancellationToken);

        var version = BinaryPrimitives.ReadUInt32BigEndian(packHeader.AsSpan(0, 4));

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
                return objectSize > 0 ? new ZLibFixedStream(stream, objectSize, null) : new MemoryStream([]);
            }
            if (objectSize > 0)
            {
                using var zlib = new ZLibFixedStream(stream, objectSize, null);
                await zlib.SkipToEndAsync(cancellationToken);
            }
        }

        throw new Exception();
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public class RequestInfo(GitSmartHttpFileProvider Provider, string Oid)
    {
        public Uri Repository => Provider.options.Repository;
    }

    record LengthWrapper(long Length);

    record struct TreeEntry(TreeObjectType Type, string Name, string Id);

    record struct ServerCapabilities(ObjectFormat ObjectFormat, bool Filter, bool Shallow)
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
        long? length = folder
            ? -1
            : (provider.blobLengths.TryGetValue(oid, out var l) ? l : null);

        public bool Exists => true;

        public long Length
        {
            get
            {
                if (length == null)
                {
                    lock (this)
                    {
                        if (length == null)
                        {
                            using var stream = Task.Run(async () => await provider.LoadStreamAsync(oid).ConfigureAwait(false))
                                .ConfigureAwait(false)
                                .GetAwaiter()
                                .GetResult();

                            length = stream.Length;
                        }
                    }
                }

                return length!.Value;
            }
        }

        public string? PhysicalPath => null;

        public string Name => name;

        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;

        public bool IsDirectory => folder;

        public Stream CreateReadStream() =>
            !IsDirectory
                ? provider.LoadStreamAsync(oid).ConfigureAwait(false).GetAwaiter().GetResult()
                : throw new FileNotFoundException();
    }
}

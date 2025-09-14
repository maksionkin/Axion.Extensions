// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IO;

namespace Axion.Extensions.Caching.Azure.Storage.Blobs;

/// <summary>
/// Distributed cache implementation using Azure Storage Blobs.
/// </summary>
public class AzureBlobsCache : IBufferDistributedCache, IDisposable
{
    static readonly int MaxKeyLength = 1024 - GetHash("").Length - 1; // -1 for  '-' character
    static readonly RecyclableMemoryStreamManager RecyclableMemoryStreamManager = new();

    readonly IOptions<AzureBlobsCacheOptions> optionsAccessor;
    readonly CancellationTokenSource? source;
    bool disposedValue;

    /// <summary>
    /// Initializes a new instance of <see cref="AzureBlobsCache"/>.
    /// </summary>
    /// <param name="optionsAccessor">The configuration options.</param>
    public AzureBlobsCache(IOptions<AzureBlobsCacheOptions> optionsAccessor)
    {
        Guard.IsNotNull(optionsAccessor);

        this.optionsAccessor = optionsAccessor;

        if (!optionsAccessor.Value.DisableBackgroundExpiredItemsDeletion)
        {
            source = new();

            Task.Factory.StartNew(() => RemoveExpiredUntilCancelledAsync(source.Token), TaskCreationOptions.LongRunning).ConfigureAwait(false);
        }
    }

    async void RemoveExpiredUntilCancelledAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var wait = optionsAccessor.Value.ExpiredItemsDeletionInterval;
            if (wait <= TimeSpan.Zero)
            {
                wait = TimeSpan.FromMinutes(30);
            }

            try
            {
                await Task.Delay(wait, cancellationToken);
            }
            catch
            {
                continue;
            }

            try
            {
                await RemoveExpiredAsync(cancellationToken);
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Remove all expired entries.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    public async ValueTask RemoveExpiredAsync(CancellationToken cancellationToken)
    {
        var containerClient = optionsAccessor.Value.GetBlobContainerClient();
        await foreach (var blob in containerClient.GetBlobsAsync(BlobTraits.Metadata, prefix: optionsAccessor.Value.BlobPrefix, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!blob.Deleted && !new CacheEntryMetadata(blob.Metadata).IsValid)
            {
                RemoveAsync(containerClient.GetBlobClient(blob.Name), blob.Properties.ETag!.Value);
            }
        }
    }

    static string GetHash(string key)
    {
        var data = Encoding.UTF8.GetBytes(key);

#if NET8_0_OR_GREATER        
        var bytes = SHA256.HashData(data);
#else
        using var hash = SHA256.Create();

        var bytes = hash.ComputeHash(data);
#endif
        return Base64Url.EncodeToString(bytes);
    }

    /// <summary>
    /// Returns a <see cref="BlobClient"/> for a given <paramref name="key"/>.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <returns>A <see cref="BlobClient"/>.</returns>
    public BlobClient GetBlobClient(string key)
    {
        Guard.IsNotNullOrEmpty(key);

        var containerClient = optionsAccessor.Value.GetBlobContainerClient();

        if (!disposedValue)
        {
            var path = new StringBuilder(optionsAccessor.Value.BlobPrefix);

            var maxSegmentCount = 254;
            if (!string.IsNullOrEmpty(optionsAccessor.Value.BlobPrefix))
            {
                maxSegmentCount -= optionsAccessor.Value.BlobPrefix.Count(c => c == '/');
            }

            var suffixWithHash = false;
            var segmentCount = 0;
            foreach (var segment in key.Split('/'))
            {
                if (segmentCount > 0)
                {
                    path.Append('/');
                }

                if (segment.Length == 0)
                {
                    path.Append('.');

                    suffixWithHash = true;
                }

                path.Append(Uri.EscapeDataString(segment));
                if (++segmentCount > maxSegmentCount || path.Length >= MaxKeyLength)
                {
                    suffixWithHash = true;

                    break;
                }
            }

            switch (path[^1])
            {
                case '/':
                case '.':
                    suffixWithHash = true;
                    break;
            }

            if (suffixWithHash)
            {
                if (MaxKeyLength < path.Length)
                {
                    path.Length = MaxKeyLength;
                }

                path.Append('-');
                path.Append(GetHash(key));
            }

            return containerClient.GetBlobClient(path.ToString());
        }

        throw new ObjectDisposedException($"{GetType().Name}: Account = {containerClient.AccountName}, Container = {containerClient.Name}");
    }

    /// <inheritdoc/>
    public byte[]? Get(string key) =>
        GetAsync(key).Result;

    static async void RemoveAsync(BlobClient blobClient, ETag etag)
    {
        try
        {
            await blobClient.DeleteAsync(conditions: new() { IfMatch = etag }).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        using var stream = RecyclableMemoryStreamManager.GetStream(key);
        if (await TryGetAsync(key, stream, token))
        {
            return stream.ToArray();
        }

        return null;
    }

    static async ValueTask<bool> CheckAndUpdateMetadataAsync(BlobClient blobClient, IDictionary<string, string> metadata, ETag etag, Func<ValueTask>? func, CancellationToken cancellationToken)
    {
        var cacheEntryMetadata = new CacheEntryMetadata(metadata);
        if (cacheEntryMetadata.IsValid)
        {
            if (func != null)
            {
                await func();
            }

            try
            {
                await blobClient.SetMetadataAsync(cacheEntryMetadata.ToDictionary(), new() { IfMatch = etag }, cancellationToken);
            }
            catch (RequestFailedException rfe) when (rfe.Status == (int)HttpStatusCode.NotFound || rfe.Status == (int)HttpStatusCode.PreconditionFailed)
            {
            }

            return true;
        }
        else
        {
            RemoveAsync(blobClient, etag);
        }

        return false;
    }

    /// <inheritdoc/>
    public void Refresh(string key) =>
        RefreshAsync(key).Wait();

    /// <inheritdoc/>
    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        try
        {
            var blobClient = GetBlobClient(key);
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: token);

            await CheckAndUpdateMetadataAsync(blobClient, properties.Value.Metadata, properties.Value.ETag, null, token);
        }
        catch (RequestFailedException rfe) when (rfe.Status == (int)HttpStatusCode.NotFound)
        {
        }
    }

    /// <inheritdoc/>
    public void Remove(string key) =>
        RemoveAsync(key).Wait();

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken token = default) =>

        await GetBlobClient(key).DeleteIfExistsAsync(cancellationToken: token).ConfigureAwait(false);

    /// <inheritdoc/>
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        SetAsync(key, value, options).Wait();

    /// <inheritdoc/>
    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Guard.IsNotNull(value);

        await SetAsync(key, new ReadOnlySequence<byte>(value), options, token);
    }

    /// <summary>
    /// Dispose the instance.
    /// </summary>
    /// <param name="disposing">true if the motod is called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                source?.Cancel(false);

                source?.Dispose();
            }

            disposedValue = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public bool TryGet(string key, IBufferWriter<byte> destination) =>
        TryGetAsync(key, destination, default).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async ValueTask<bool> TryGetAsync(string key, IBufferWriter<byte> destination, CancellationToken token = default)
    {
        try
        {
            var blobClient = GetBlobClient(key);
            var download = await blobClient.DownloadAsync(token);

            return await CheckAndUpdateMetadataAsync(blobClient,
                download.Value.Details.Metadata,
                download.Value.Details.ETag,
                async () =>
                {
                    using var content = download.Value.Content;
                    using var stream = destination.AsStream();
                    await content.CopyToAsync(stream, 8 * 1024, token);
                },
                token);
        }
        catch (RequestFailedException rfe) when (rfe.Status == (int)HttpStatusCode.NotFound)
        {
        }

        return false;
    }

    /// <inheritdoc/>
    public void Set(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options) =>
        SetAsync(key, value, options).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async ValueTask SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Guard.IsNotNull(options);

        var metadata = new CacheEntryMetadata(
            options.AbsoluteExpiration
                ?? (DateTimeOffset.UtcNow + options.AbsoluteExpirationRelativeToNow),
            options.SlidingExpiration,
            DateTimeOffset.UtcNow);

        if (metadata.IsValid)
        {
            var blobClient = GetBlobClient(key);

            while (true)
            {
                try
                {
                    using var stream = value.AsStream();

                    await blobClient.UploadAsync(stream,
                        new BlobUploadOptions() { Metadata = metadata.ToDictionary() },
                        token).ConfigureAwait(false);

                    break;
                }
                catch (RequestFailedException rfe) when (optionsAccessor.Value.CreateContainerIfNotExists && rfe.Status == (int)HttpStatusCode.NotFound && rfe.ErrorCode == BlobErrorCode.ContainerNotFound)
                {
                    await optionsAccessor.Value.GetBlobContainerClient().CreateIfNotExistsAsync(cancellationToken: token);
                }
            }
        }
        else
        {
            await RemoveAsync(key, token);
        }
    }

    readonly record struct CacheEntryMetadata(DateTimeOffset? AbsoluteExpiration, TimeSpan? SlidingExpiration, DateTimeOffset LastAccessed)
    {
        public CacheEntryMetadata(IDictionary<string, string> metadata)
            : this(GetDateTimeOffset(metadata, nameof(AbsoluteExpiration)),
                  GetTimeSpan(metadata, nameof(SlidingExpiration)),
                  GetDateTimeOffset(metadata, nameof(LastAccessed)) ?? default)
        {
        }

        delegate bool TryParseFunc<T>(string stringValue, out T value) where T : struct;

        static bool TryParse(string stringValue, out DateTimeOffset value) =>
            DateTimeOffset.TryParseExact(stringValue, "O", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value);

        static bool TryParse(string stringValue, out TimeSpan value) =>
            TimeSpan.TryParseExact(stringValue, "c", CultureInfo.InvariantCulture, out value);

        static T? GetValue<T>(IDictionary<string, string> metadata, string name, TryParseFunc<T> getValue)
            where T : struct =>
                 metadata.TryGetValue(name, out var stringValue)
                        && getValue(stringValue, out var value)
                    ? value
                    : null;

        static DateTimeOffset? GetDateTimeOffset(IDictionary<string, string> metadata, string name) =>
            GetValue<DateTimeOffset>(metadata, name, TryParse);

        static TimeSpan? GetTimeSpan(IDictionary<string, string> metadata, string name) =>
            GetValue<TimeSpan>(metadata, name, TryParse);

        public DateTimeOffset ExpiresAt
        {
            get
            {
                if (SlidingExpiration == null)
                {
                    return AbsoluteExpiration ?? LastAccessed;
                }
                else
                {
                    var expiresAt = LastAccessed + SlidingExpiration.Value;

                    return AbsoluteExpiration == null || AbsoluteExpiration.Value > expiresAt
                        ? expiresAt
                        : AbsoluteExpiration.Value;
                }
            }
        }

        public bool IsValid =>
            ExpiresAt >= DateTimeOffset.UtcNow;

        static string ToString(DateTimeOffset value) =>
            value.ToString("O", CultureInfo.InvariantCulture);

        public IDictionary<string, string> ToDictionary()
        {
            var metadata = new Dictionary<string, string>
            {
                [nameof(LastAccessed)] = ToString(DateTimeOffset.UtcNow)
            };

            if (AbsoluteExpiration != null)
            {
                metadata.Add(nameof(AbsoluteExpiration), ToString(AbsoluteExpiration.Value));
            }

            if (SlidingExpiration != null)
            {
                metadata.Add(nameof(SlidingExpiration), SlidingExpiration.Value.ToString("c", CultureInfo.InvariantCulture));
            }

            return metadata;
        }
    }
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using Axion.Extensions.Caching.Transformed;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up <see cref="TransformedCache"/> distributed cache related services in an <see cref="IServiceCollection" />.
/// </summary>
public static class TransformedCacheServiceCollectionExtensions
{
    static readonly ConcurrentDictionary<Type, (Func<Stream, CompressionLevel, Stream> Compress, Func<Stream, Stream> Decompress)?> cachedCompressionMethods = new();


    /// <summary>
    /// Adds <see cref="TransformedCache"/> distributed caching services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="encode">Encode transform.</param>
    /// <param name="decode">Decode transform.</param>
    /// <param name="convertCacheKey">Cache key converter.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="InvalidOperationException">When no <see cref="IDistributedCache"/> was added to <paramref name="services"/>.</exception>
    public static IServiceCollection AddTransformedCache(this IServiceCollection services, Func<byte[], byte[]> encode, Func<byte[], byte[]> decode, Func<string, string>? convertCacheKey = null)
    {
        Guard.IsNotNull(services);
        Guard.IsNotNull(encode);
        Guard.IsNotNull(decode);

        var descriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IDistributedCache))
            ?? throw new InvalidOperationException("No instance of IDistributedCache found.");

        var createCacheProvider = descriptor.ImplementationFactory;
        createCacheProvider ??= serviceProvider => ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!);

        services.Remove(descriptor);

        services.Add(new(descriptor.ServiceType,
            serviceProvider =>
            {
                var options = new TransformedCacheOptions
                {
                    CacheProvider = (IDistributedCache)createCacheProvider(serviceProvider),
                    Encode = encode,
                    Decode = decode
                };

                if (convertCacheKey != null)
                {
                    options.ConvertCacheKey = convertCacheKey;
                }

                return new TransformedCache(options);
            },
            descriptor.Lifetime));

        return services;
    }

    /// <summary>
    /// Get a pair of <see cref="T:Func{Byte[], Byte[]}"/> to comress/decompress.
    /// </summary>
    /// <param name="compressionStreamType">An implementation of compression <see cref="Stream"/>.</param>
    /// <param name="compressionLevel">A <see cref="CompressionLevel"/> to compress.</param>
    /// <returns>A pair of <see cref="T:Func{Byte[], Byte[]}"/> to comress/decompress.</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static (Func<byte[], byte[]> Encode, Func<byte[], byte[]> Decode) GetTransformMethods(Type compressionStreamType, CompressionLevel compressionLevel = default)
    {
        Guard.IsNotNull(compressionStreamType);

        if (!typeof(Stream).IsAssignableFrom(compressionStreamType))
        {
            throw new ArgumentOutOfRangeException(nameof(compressionStreamType), $"{compressionStreamType} should derive from {typeof(Stream)}.");
        }

        static (Func<Stream, CompressionLevel, Stream> Compress, Func<Stream, Stream> Decompress)? CreateCompressionMethods(Type compressionStreamType)
        {
            var stream = Expression.Parameter(typeof(Stream));

            var ctor = compressionStreamType.GetConstructor([stream.Type, typeof(CompressionMode)]);
            if (ctor == null)
            {
                return default;
            }

            var decompress = Expression.Lambda<Func<Stream, Stream>>(Expression.New(ctor, stream, Expression.Constant(CompressionMode.Decompress)), stream).Compile();

            var compressionLevel = Expression.Parameter(typeof(CompressionLevel));

            var compressCtor = compressionStreamType.GetConstructor([stream.Type, compressionLevel.Type]);

            var compress = Expression.Lambda<Func<Stream, CompressionLevel, Stream>>(
                (compressCtor == null
                    ? Expression.New(ctor, stream, Expression.Constant(CompressionMode.Compress))
                    : Expression.New(compressCtor, stream, compressionLevel)),
                stream, compressionLevel).Compile();

            return (compress, decompress);
        }

        var compressionMethods = cachedCompressionMethods.GetOrAdd(compressionStreamType, CreateCompressionMethods);

        return compressionMethods == null
            ? throw new ArgumentOutOfRangeException(nameof(compressionStreamType), $"{nameof(compressionStreamType)} should have compression constructors.")
            : (bytes =>
                {
                    using var compressedStream = new MemoryStream();
                    {
                        using var compressionStream = compressionMethods.Value.Compress(compressedStream, compressionLevel);
                        compressionStream.Write(bytes, 0, bytes.Length);
                    }

                    return compressedStream.ToArray();
                },
                bytes =>
                {
                    using var memoryStream = new MemoryStream();
                    {
                        using var compressedStream = new MemoryStream(bytes);
                        using var compressionStream = compressionMethods.Value.Decompress(compressedStream);
                        compressionStream.CopyTo(memoryStream);
                    }

                    return memoryStream.ToArray();
                }
        );
    }

    /// <summary>
    /// Adds compression <see cref="TransformedCache"/> distributed caching services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="compressionLevel">A <see cref="CompressionLevel"/> to compress.</param>
    /// <param name="convertCacheKey">Cache key converter.</param>
    /// <typeparam name="TCompressionStream">An implentation of comression stream.
    /// <list type="bullet|number|table">
    /// <listheader>
    /// <operation>Operation</operation>
    /// <signature>Constructor signature</signature>
    /// </listheader>
    /// <item>
    /// <item>
    /// <operation>Decompress</operation>
    /// <signature>(<see cref="Stream"/>, <see cref="CompressionMode.Decompress"/>)</signature>
    /// </item>
    /// <operation>Compress</operation>
    /// <signature>(<see cref="Stream"/>, <see cref="CompressionLevel"/> <paramref name="compressionLevel"/>)</signature>
    /// </item>
    /// <item>
    /// <operation>Compress (alternative)</operation>
    /// <signature>(<see cref="Stream"/>, <see cref="CompressionMode.Compress"/>)</signature>
    /// </item>
    /// </list>
    /// </typeparam>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="InvalidOperationException">When no singleton <see cref="IDistributedCache"/> was added to <paramref name="services"/>.</exception>
    public static IServiceCollection AddTransformedCache<TCompressionStream>(this IServiceCollection services, CompressionLevel compressionLevel = default, Func<string, string>? convertCacheKey = null)
        where TCompressionStream : Stream
    {
        Guard.IsNotNull(services);

        var (encode, decode) = GetTransformMethods(typeof(TCompressionStream), compressionLevel);

        return services.AddTransformedCache(encode, decode, convertCacheKey);
    }

    /// <summary>
    /// Get a <see cref="T:Func{Byte[], Byte[]}"/> to transform.
    /// </summary>
    /// <param name="transformFactory">A <see cref="Func{ICryptoTransform}"/>.</param>
    /// <returns>A <see cref="T:Func{Byte[], Byte[]}"/> to trasform.</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static Func<byte[], byte[]> GetTransformMethod(Func<ICryptoTransform> transformFactory)
    {
        Guard.IsNotNull(transformFactory);

        return bytes =>
            {
                using var encryptedStream = new MemoryStream();
                {
                    using var cryptoStream = new CryptoStream(encryptedStream, transformFactory(), CryptoStreamMode.Write);
                    cryptoStream.Write(bytes, 0, bytes.Length);
                }

                return encryptedStream.ToArray();
            };
    }

    /// <summary>
    /// Adds encryption <see cref="TransformedCache"/> distributed caching services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="encryptTransformFactory">A <see cref="ICryptoTransform"/> to encrypt.</param>
    /// <param name="decryptTransformFactory">A <see cref="ICryptoTransform"/> to decrypt.</param>
    /// <param name="convertCacheKey">Cache key converter.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddTransformedCache(this IServiceCollection services, Func<ICryptoTransform> encryptTransformFactory, Func<ICryptoTransform> decryptTransformFactory, Func<string, string>? convertCacheKey = null)
    {
        Guard.IsNotNull(services);
        Guard.IsNotNull(encryptTransformFactory);
        Guard.IsNotNull(decryptTransformFactory);

        return services.AddTransformedCache(GetTransformMethod(encryptTransformFactory), GetTransformMethod(decryptTransformFactory), convertCacheKey);
    }

    /// <summary>
    /// Adds encryption <see cref="TransformedCache"/> distributed caching services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="algorithm">A <see cref="SymmetricAlgorithm"/> to encrypt/decrypt.</param>
    /// <param name="convertCacheKey">Cache key converter.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddTransformedCache(this IServiceCollection services, SymmetricAlgorithm algorithm, Func<string, string>? convertCacheKey = null)
    {
        Guard.IsNotNull(services);
        Guard.IsNotNull(algorithm);

        return services.AddTransformedCache(algorithm.CreateEncryptor, algorithm.CreateDecryptor, convertCacheKey);
    }
}

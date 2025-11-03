// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System.Linq;
using System.Text.Json;
using Axion.Azure.Functions.Worker.Converters.Providers;
using Azure.Core.Serialization;
using CommunityToolkit.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for the <see cref="IServiceProvider"/> interface to retrieve serializers and converters.
/// </summary>
/// <remarks>This class includes methods to obtain an <see cref="ObjectSerializer"/> and asynchronous converters
/// from a service provider. It is designed to facilitate the retrieval of these components, which are typically
/// registered in the service collection.</remarks>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// Gets the configured <see cref="ObjectSerializer"/> from the service provider.
    /// </summary>
    /// <remarks>
    /// The method first attempts to obtain the serializer configured on <see cref="WorkerOptions"/>.
    /// If no serializer is configured, it falls back to creating a <see cref="JsonObjectSerializer"/>
    /// using registered <see cref="JsonSerializerOptions"/>, or to <see cref="JsonObjectSerializer.Default"/>
    /// when no options are available.
    /// </remarks>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
    /// <returns>An <see cref="ObjectSerializer"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public static ObjectSerializer GetObjectSerializer(this IServiceProvider serviceProvider)
    {
        Guard.IsNotNull(serviceProvider);

        var serializer = serviceProvider.GetService<IOptionsMonitor<WorkerOptions>>()?.CurrentValue.Serializer;
        if (serializer == null)
        {
            var jsonSerializerOptions = serviceProvider.GetService<IOptionsMonitor<JsonSerializerOptions>>()?.CurrentValue;

            serializer = jsonSerializerOptions == null
                ? JsonObjectSerializer.Default
                : new JsonObjectSerializer(jsonSerializerOptions);
        }

        return serializer;
    }

    /// <summary>
    /// Attempts to resolve an <see cref="IAsyncConverter{TInput,TOutput}"/> from the service provider.
    /// </summary>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
    /// <typeparam name="TInput">The source type to convert from.</typeparam>
    /// <typeparam name="TOutput">The destination type to convert to.</typeparam>
    /// <returns>
    /// An instance of <see cref="IAsyncConverter{TInput,TOutput}"/> if a matching converter is registered;
    /// otherwise <see langword="null"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public static IAsyncConverter<TInput, TOutput>? GetAsyncConverter<TInput, TOutput>(this IServiceProvider serviceProvider) =>
        serviceProvider.GetAsyncConverter(typeof(TInput), typeof(TOutput)) as IAsyncConverter<TInput, TOutput>;

    /// <summary>
    /// Locates a converter for the specified runtime <paramref name="input"/> and <paramref name="output"/> types
    /// by querying registered <see cref="IAsyncConverterProvider"/> services.
    /// </summary>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
    /// <param name="input">The runtime <see cref="Type"/> to convert from.</param>
    /// <param name="output">The runtime <see cref="Type"/> to convert to.</param>
    /// <param name="skipFilter">
    /// An optional predicate used to filter candidate providers. If supplied, providers for which the predicate
    /// returns <see langword="true"/> will be skipped. If <see langword="null"/>, all providers are considered.
    /// </param>
    /// <returns>
    /// A closed-generic converter object that implements <see cref="IAsyncConverter{TInput,TOutput}"/> for the
    /// provided types, or <see langword="null"/> if no suitable converter is found.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="serviceProvider"/>, <paramref name="input"/>, or <paramref name="output"/> is <see langword="null"/>.
    /// </exception>
    public static object? GetAsyncConverter(this IServiceProvider serviceProvider, Type input, Type output, Func<IAsyncConverterProvider, bool>? skipFilter = null)
    {
        Guard.IsNotNull(serviceProvider);
        Guard.IsNotNull(input);
        Guard.IsNotNull(output);

        var outputType = typeof(IAsyncConverter<,>).MakeGenericType(input, output);
        foreach (var item in serviceProvider.GetServices<IAsyncConverterProvider>()
            .Where(p => skipFilter?.Invoke(p) != true)
            .Select(p => new { Provider = p, AsyncConverter = p.GetAsyncConverter(input, output) })
            .Where(x => x.AsyncConverter is object && outputType.IsInstanceOfType(x.AsyncConverter)))
        {
            return item.AsyncConverter;
        }

        return null;
    }
}

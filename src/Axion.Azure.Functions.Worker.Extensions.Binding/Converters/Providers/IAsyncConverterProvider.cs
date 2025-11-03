// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker.Converters.Providers;

/// <summary>
/// Resolves an asynchronous converter for a pair of runtime <see cref="Type"/>'s.
/// Implementations return a closed generic instance of <see cref="IAsyncConverter{TInput,TOutput}"/>
/// when they can produce a converter for the specified input and output types, or <see langword="null"/>
/// if no converter is available. Used by the binding infrastructure to perform runtime conversions.
/// </summary>
public interface IAsyncConverterProvider
{
    /// <summary>
    /// Gets an instance of <see cref="IAsyncConverter{TInput,TOutput}"/>.
    /// </summary>
    /// <param name="input">The <see cref="Type"/> to convert from.</param>
    /// <param name="output">The <see cref="Type"/> to convert to.</param>
    /// <returns>An instance of <see cref="IAsyncConverter{TInput,TOutput}"/> or <see langword="null"/>.</returns>
    object? GetAsyncConverter(Type input, Type output);
}

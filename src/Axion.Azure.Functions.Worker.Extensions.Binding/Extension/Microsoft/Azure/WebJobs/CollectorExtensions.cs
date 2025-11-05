// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Azure.WebJobs;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension helpers for converting synchronous <see cref="ICollector{T}"/> instances
/// into their asynchronous counterpart <see cref="IAsyncCollector{T}"/>.
/// </summary>
public static class CollectorExtensions
{
    /// <summary>
    /// Wraps the provided <see cref="ICollector{T}"/> in an <see cref="IAsyncCollector{T}"/>
    /// adapter that forwards synchronous <see cref="ICollector{T}.Add(T)"/> calls to
    /// <see cref="IAsyncCollector{T}.AddAsync(T, CancellationToken)"/>.
    /// </summary>
    /// <param name="collector">The synchronous collector to adapt. Cannot be <see langword="null"/>.</param>
    /// <returns>
    /// An <see cref="IAsyncCollector{T}"/> that delegates additions to the supplied synchronous collector.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="collector"/> is <see langword="null"/>.</exception>
    public static IAsyncCollector<T> ToAsyncCollector<T>(this ICollector<T> collector)
    {
        ArgumentNullException.ThrowIfNull(collector);

        return new SynchronousToAsynchronousCollector<T>(collector);
    }

    class SynchronousToAsynchronousCollector<T>(ICollector<T> collector)
        : IAsyncCollector<T>
    {
        public Task AddAsync(T item, CancellationToken cancellationToken = default)
        {
            collector.Add(item);

            return Task.CompletedTask;
        }
        public Task FlushAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

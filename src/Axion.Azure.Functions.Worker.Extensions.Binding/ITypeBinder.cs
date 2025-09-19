// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker;

/// <summary>
/// Defines a contract for creating binding instances of a specified type.
/// </summary>
/// <remarks>This interface extends <see cref="IBinder"/> to provide functionality for asynchronously creating
/// bindings of a specific type.</remarks>
public interface ITypeBinder : IBinder
{
    /// <summary>
    /// Creates a binding instance with the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the binding.</typeparam>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>An instance of the specified binding type.</returns>
    ValueTask<T> BindAsync<T>(CancellationToken cancellationToken = default);
}

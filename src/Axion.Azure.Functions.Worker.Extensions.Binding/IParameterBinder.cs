// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker;

/// <summary>
/// Extension of <see cref="IBinder"/> to support parameter binding.
/// </summary>
public interface IParameterBinder : ITypeBinder
{
    /// <summary>
    /// Creates a binding instance with the specified parameters.
    /// </summary>
    /// <typeparam name="T">The type of the binding.</typeparam>
    /// <param name="parameterName">The parameter name of the current function.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>An instance of the specified binding type.</returns>
    ValueTask<T> BindAsync<T>(string? parameterName = null, CancellationToken cancellationToken = default);
}

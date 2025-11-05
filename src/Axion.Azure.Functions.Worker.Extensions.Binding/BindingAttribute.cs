// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker;

/// <summary>
/// Base class for output binding attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
public abstract class BindingAttribute(ImmutableArray<Type> supportedTypes) : Attribute
{
    /// <summary>
    /// Binds to the specified type.
    /// </summary>
    /// <paramref name="serviceProvider">The <see cref="IServiceProvider"/> to access dependent services.</paramref>
    /// <paramref name="type">The <see cref="Type"/> to bind to.</paramref>
    /// <paramref name="cancellationToken">The <see cref="CancellationToken"/>.</paramref>
    /// <returns>The bound value.</returns>
    protected abstract ValueTask<object?> BindAsync(IServiceProvider serviceProvider, Type type, CancellationToken cancellationToken);

    internal async ValueTask<object?> TryBindAsync(IServiceProvider serviceProvider, Type type, CancellationToken cancellationToken)
    {
        if (supportedTypes.Contains(type)
            || (type.IsGenericType && supportedTypes.Contains(type.GetGenericTypeDefinition())))
        {
            return await BindAsync(serviceProvider, type, cancellationToken).ConfigureAwait(false);
        }

        foreach (var supportedType in supportedTypes)
        {
            var converter = serviceProvider.GetAsyncConverter(supportedType, type);

            if (converter != null)
            {
                var helperType = typeof(ConvertHelper<,>).MakeGenericType(supportedType, type);
                var helper = (IConverterHelper)Activator.CreateInstance(helperType, converter)!;
                var boundValue = await BindAsync(serviceProvider, supportedType, cancellationToken).ConfigureAwait(false);

                if (boundValue != null)
                {
                    return await helper.ConvertAsync(boundValue, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return null;
    }

    interface IConverterHelper
    {
        ValueTask<object> ConvertAsync(object input, CancellationToken cancellationToken);
    }

    class ConvertHelper<TSource, TTarget>(IAsyncConverter<TSource, TTarget> converter)
        : IConverterHelper
    {
        public async ValueTask<object> ConvertAsync(object input, CancellationToken cancellationToken) =>
            (await converter.ConvertAsync((TSource)input, cancellationToken))!;
    }
}

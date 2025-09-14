// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Axion.Azure.Functions.Worker;

/// <summary>
/// Base class for output binding attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
public abstract class BindingAttribute(ImmutableArray<Type> supportedTypes) : Attribute
{
    internal static readonly ImmutableDictionary<Type, (bool ConversionDirection, Type ConvertedType, ImmutableArray<Type> Types)> GenericTypes = GetGenericTypes();

    static ImmutableDictionary<Type, (bool ConversionDirection, Type ConvertedType, ImmutableArray<Type> Types)> GetGenericTypes()
    {
        var types = new List<(Type Type, bool ConversionDirection, Type ConvertedType)>
        {
            (typeof(IAsyncCollector<>), true, typeof(ConvertedAsyncCollector<,>)),
            (typeof(ICollector<>), true, typeof(ConvertedAsyncCollector<,>)),
            (typeof(IAsyncEnumerable<>), false, typeof(ConvertedAsyncEnumerable<,>)),
            (typeof(IEnumerable<>), false, typeof(ConvertedAsyncEnumerable<,>))
        };

        return types.ToImmutableDictionary(
            v => v.Type,
            v => (v.ConversionDirection,
                v.ConvertedType,
                types.Where(t => t.ConversionDirection == v.ConversionDirection)
                .Select(t => t.Type)
                .ToImmutableArray()));
    }

    /// <summary>
    /// Binds to the specified type.
    /// </summary>
    /// <paramref name="serviceProvider">The <see cref="IServiceProvider"/> to access dependent services.</paramref>
    /// <paramref name="type">The <see cref="Type"/> to bind to.</paramref>
    /// <paramref name="cancellationToken">The <see cref="CancellationToken"/>.</paramref>
    /// <returns>The bound value.</returns>
    protected abstract ValueTask<object> BindAsync(IServiceProvider serviceProvider, Type type, CancellationToken cancellationToken);

    internal async ValueTask<object?> TryBindAsync(IServiceProvider serviceProvider, Type type, CancellationToken cancellationToken)
    {
        if (supportedTypes.Contains(type)
            || (type.IsGenericType && supportedTypes.Contains(type.GetGenericTypeDefinition())))
        {
            return await BindAsync(serviceProvider, type, cancellationToken).ConfigureAwait(false);
        }
        else if (supportedTypes.FirstOrDefault(type.IsAssignableFrom) is Type) // Check for derived types
        {
            return await BindAsync(serviceProvider, type, cancellationToken).ConfigureAwait(false);

        }
        else // Check for custom converters
        {
            IConverterHelper? GetConverterHelper(Type input, Type output)
            {
                try
                {
                    return (IConverterHelper)ActivatorUtilities.CreateInstance(
                        serviceProvider,
                        typeof(ConvertHelper<,>).MakeGenericType(input, output));
                }
                catch { }

                return null;
            }

            foreach (var supportedType in supportedTypes)
            {
                var convertHelper = GetConverterHelper(supportedType, type);

                if (convertHelper != null)
                {
                    var target = await BindAsync(serviceProvider, supportedType, cancellationToken).ConfigureAwait(false);

                    return await convertHelper.ConvertAsync(target, cancellationToken).ConfigureAwait(false);
                }
            }

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                if (GenericTypes.TryGetValue(definition, out var convert))
                {
                    foreach (var definitionType in convert.Types.Except([definition]).Prepend(definition))
                    {
                        foreach (var supportedType in supportedTypes.Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == definitionType))
                        {
                            var typeArgument = type.GetGenericArguments()[0];
                            var supportedTypeArgument = supportedType.GetGenericArguments()[0];

                            var (from, to) = convert.ConversionDirection
                                ? (typeArgument, supportedTypeArgument)
                                : (supportedTypeArgument, typeArgument);

                            var convertHelper = GetConverterHelper(from, to);
                            if (convertHelper != null)
                            {
                                var target = await BindAsync(serviceProvider, supportedType, cancellationToken).ConfigureAwait(false);

                                return ActivatorUtilities.CreateInstance(
                                    serviceProvider,
                                    convert.ConvertedType.MakeGenericType(typeArgument, supportedTypeArgument),
                                    target);
                            }
                        }
                    }
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
            await converter.ConvertAsync((TSource)input, cancellationToken);
    }
}

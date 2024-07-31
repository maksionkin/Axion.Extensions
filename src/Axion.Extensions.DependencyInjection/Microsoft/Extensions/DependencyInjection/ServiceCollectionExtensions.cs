// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using static Axion.Extensions.DependencyInjection.ExpressionHelpers;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/>.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    static readonly ConcurrentDictionary<MethodInfo, (Func<object?, IServiceProvider, object> Factory, Type ImplementationType)> cached = new();

    static IServiceCollection Add(this IServiceCollection services, Delegate implementationFactory, Func<IServiceCollection, Type, Func<IServiceProvider, object>, IServiceCollection> add, Type? serviceType)
    {
        Guard.IsNotNull(services);
        Guard.IsNotNull(implementationFactory);

        var (factory, implementationType) = cached.GetOrAdd(implementationFactory.GetMethodInfo(), Create);

        return add(services, serviceType ?? implementationType, provider => factory(implementationFactory.Target, provider));
    }

    static void TryAdd(this IServiceCollection services, Delegate implementationFactory, Action<IServiceCollection, Type, Func<IServiceProvider, object>> tryAdd, Type? serviceType) =>
        services.Add(
            implementationFactory,
            (sc, t, f) =>
            {
                tryAdd(sc, t, f);

                return sc;
            },
            serviceType);

    static (Func<object?, IServiceProvider, object> Factory, Type ImplementationType) Create(MethodInfo method)
    {
        if (method.ReturnType == typeof(void))
        {
            throw new InvalidOperationException("The delegate must have non void return value.");
        }

        var target = Expression.Parameter(typeof(object));
        var provider = Expression.Parameter(typeof(IServiceProvider));

        var call = GetMethodCallExpression(method, target, provider);

        return ((Expression.Lambda(call, target, provider).Compile() as Func<object?, IServiceProvider, object>)!, method.ReturnType);
    }
}

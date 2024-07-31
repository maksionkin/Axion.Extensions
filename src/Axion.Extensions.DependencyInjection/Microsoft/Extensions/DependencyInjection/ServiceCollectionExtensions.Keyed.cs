// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using CommunityToolkit.Diagnostics;

using static Axion.Extensions.DependencyInjection.ExpressionHelpers;

namespace Microsoft.Extensions.DependencyInjection;

partial class ServiceCollectionExtensions
{
    static readonly ConcurrentDictionary<MethodInfo, (Func<object?, object?, IServiceProvider, object> Factory, Type ImplementationType)> cachedKeyed = new();

    static IServiceCollection AddKeyed(this IServiceCollection services, object? serviceKey, Delegate implementationFactory, Func<IServiceCollection, Type, object?, Func<IServiceProvider, object?, object>, IServiceCollection> add, Type? serviceType)
    {
        Guard.IsNotNull(services);
        Guard.IsNotNull(implementationFactory);

        var (factory, implementationType) = cachedKeyed.GetOrAdd(implementationFactory.GetMethodInfo(), CreateKeyed);

        return add(services, serviceType ?? implementationType, serviceKey, (provider, key) => factory(implementationFactory.Target, key, provider));
    }

    static void TryAddKeyed(this IServiceCollection services, object? serviceKey, Delegate implementationFactory, Action<IServiceCollection, Type, object?, Func<IServiceProvider, object?, object>> tryAdd, Type? serviceType) =>
        services.AddKeyed(
            serviceKey,
            implementationFactory,
            (sc, t, k, f) =>
            {
                tryAdd(sc, t, k, f);

                return sc;
            },
            serviceType);

    static (Func<object?, object?, IServiceProvider, object> Factory, Type ImplementationType) CreateKeyed(MethodInfo method)
    {
        if (method.ReturnType == typeof(void))
        {
            throw new InvalidOperationException("The delegate must have non void return value.");
        }

        var target = Expression.Parameter(typeof(object));
        var objectKey = Expression.Parameter(typeof(object));
        var provider = Expression.Parameter(typeof(IServiceProvider));

        var call = GetMethodCallExpression(method, target, provider, objectKey);

        return ((Expression.Lambda(call, target, objectKey, provider).Compile() as Func<object?, object?, IServiceProvider, object>)!, method.ReturnType);
    }
}

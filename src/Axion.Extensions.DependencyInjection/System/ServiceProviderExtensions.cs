// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using static Axion.Extensions.DependencyInjection.ExpressionHelpers;

namespace System;

/// <summary>
/// <see cref="IServiceProvider"/> extensions.
/// </summary>
public static partial class ServiceProviderExtensions
{
    static readonly ConcurrentDictionary<MethodInfo, Func<object?, IServiceProvider, object>> cached = new();
    static readonly ConcurrentDictionary<MethodInfo, Action<object?, IServiceProvider>> cachedActions = new();

    /// <summary>
    /// Executes a <paramref name="delegate"/> in context of <see cref="IServiceProvider"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the return value of the method that <paramref name="delegate"/> encapsulates.</typeparam>
    /// <param name="serviceProvider">a <see cref="IServiceProvider"/>.</param>
    /// <param name="delegate">A delegate that is executed in context of <paramref name="serviceProvider"/>.</param>
    /// <returns>A value that returns <paramref name="delegate"/>.</returns>
    public static TResult Execute<TResult>(this IServiceProvider serviceProvider, Delegate @delegate)
    {
        Guard.IsNotNull(serviceProvider);
        Guard.IsNotNull(@delegate);

        var method = @delegate.GetMethodInfo();

        if (method.ReturnType == typeof(void))
        {
            throw new ArgumentOutOfRangeException(nameof(@delegate), "Delegate must have non void return type.");
        }

        if (!typeof(TResult).IsAssignableFrom(method.ReturnType))
        {
            throw new ArgumentOutOfRangeException(nameof(@delegate), $"Delegate return type must be assginable to {nameof(TResult)} ({typeof(TResult).FullName}).");

        }

        var factory = cached.GetOrAdd(@delegate.GetMethodInfo(), Create<Func<object?, IServiceProvider, object>>);

        return (TResult)factory(@delegate.Target, serviceProvider);
    }

    /// <summary>
    /// Executes a <paramref name="delegate"/> in context of <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">a <see cref="IServiceProvider"/>.</param>
    /// <param name="delegate">A delegate that is executed in context of <paramref name="serviceProvider"/>.</param>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void Execute(this IServiceProvider serviceProvider, Delegate @delegate)
    {
        Guard.IsNotNull(serviceProvider);
        Guard.IsNotNull(@delegate);

        var factory = cachedActions.GetOrAdd(@delegate.GetMethodInfo(), Create<Action<object?, IServiceProvider>>);

        factory(@delegate.Target, serviceProvider);
    }

    static TDelegate Create<TDelegate>(MethodInfo method)
        where TDelegate : Delegate
    {
        var target = Expression.Parameter(typeof(object));
        var provider = Expression.Parameter(typeof(IServiceProvider));

        var call = GetMethodCallExpression(method, target, provider);

        return (TDelegate)(Expression.Lambda(call, target, provider).Compile());
    }
}

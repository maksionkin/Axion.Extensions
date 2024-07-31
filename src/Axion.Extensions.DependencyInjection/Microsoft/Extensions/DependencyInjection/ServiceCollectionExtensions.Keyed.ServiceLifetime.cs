﻿// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.
// <auto-generated />

#nullable enable

using System;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a scoped keyed service of the type specified in <paramref name="serviceType"/> with a factory specified in <paramref name="implementationFactory"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="serviceKey">The <see cref="ServiceDescriptor.ServiceKey"/> of the service.</param>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    /// <seealso cref="ServiceLifetime.Scoped"/>
    public static IServiceCollection AddKeyedScoped(this IServiceCollection services, object? serviceKey, Delegate implementationFactory, Type? serviceType = null) =>
        services.AddKeyed(serviceKey, implementationFactory, ServiceCollectionServiceExtensions.AddKeyedScoped, serviceType);

    /// <summary>
    /// Adds a scoped keyed service of the type specified in <paramref name="serviceType"/> with a factory specified in <paramref name="implementationFactory"/> to the specified <see cref="IServiceCollection"/> if te service has not beed added yet.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="serviceKey">The <see cref="ServiceDescriptor.ServiceKey"/> of the service.</param>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <seealso cref="ServiceLifetime.Scoped"/>
    public static void TryAddKeyedScoped(this IServiceCollection services, object? serviceKey, Delegate implementationFactory, Type? serviceType = null) =>
        services.TryAddKeyed(serviceKey, implementationFactory, ServiceCollectionDescriptorExtensions.TryAddKeyedScoped, serviceType);

    /// <summary>
    /// Adds a singleton keyed service of the type specified in <paramref name="serviceType"/> with a factory specified in <paramref name="implementationFactory"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="serviceKey">The <see cref="ServiceDescriptor.ServiceKey"/> of the service.</param>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    /// <seealso cref="ServiceLifetime.Singleton"/>
    public static IServiceCollection AddKeyedSingleton(this IServiceCollection services, object? serviceKey, Delegate implementationFactory, Type? serviceType = null) =>
        services.AddKeyed(serviceKey, implementationFactory, ServiceCollectionServiceExtensions.AddKeyedSingleton, serviceType);

    /// <summary>
    /// Adds a singleton keyed service of the type specified in <paramref name="serviceType"/> with a factory specified in <paramref name="implementationFactory"/> to the specified <see cref="IServiceCollection"/> if te service has not beed added yet.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="serviceKey">The <see cref="ServiceDescriptor.ServiceKey"/> of the service.</param>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <seealso cref="ServiceLifetime.Singleton"/>
    public static void TryAddKeyedSingleton(this IServiceCollection services, object? serviceKey, Delegate implementationFactory, Type? serviceType = null) =>
        services.TryAddKeyed(serviceKey, implementationFactory, ServiceCollectionDescriptorExtensions.TryAddKeyedSingleton, serviceType);

    /// <summary>
    /// Adds a transient keyed service of the type specified in <paramref name="serviceType"/> with a factory specified in <paramref name="implementationFactory"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="serviceKey">The <see cref="ServiceDescriptor.ServiceKey"/> of the service.</param>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    /// <seealso cref="ServiceLifetime.Transient"/>
    public static IServiceCollection AddKeyedTransient(this IServiceCollection services, object? serviceKey, Delegate implementationFactory, Type? serviceType = null) =>
        services.AddKeyed(serviceKey, implementationFactory, ServiceCollectionServiceExtensions.AddKeyedTransient, serviceType);

    /// <summary>
    /// Adds a transient keyed service of the type specified in <paramref name="serviceType"/> with a factory specified in <paramref name="implementationFactory"/> to the specified <see cref="IServiceCollection"/> if te service has not beed added yet.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="serviceKey">The <see cref="ServiceDescriptor.ServiceKey"/> of the service.</param>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <seealso cref="ServiceLifetime.Transient"/>
    public static void TryAddKeyedTransient(this IServiceCollection services, object? serviceKey, Delegate implementationFactory, Type? serviceType = null) =>
        services.TryAddKeyed(serviceKey, implementationFactory, ServiceCollectionDescriptorExtensions.TryAddKeyedTransient, serviceType);

}
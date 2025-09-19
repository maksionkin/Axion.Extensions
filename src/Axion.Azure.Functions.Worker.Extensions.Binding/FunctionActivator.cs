// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace Axion.Azure.Functions.Worker;

class FunctionActivator(IFunctionActivator inner, IFunctionContextAccessor functionContextAccessor) : IFunctionActivator
{
    public object? CreateInstance(Type instanceType, FunctionContext context)
    {
        Guard.IsNotNull(instanceType);
        Guard.IsNotNull(context);

        functionContextAccessor.FunctionContext = context;

        context.InstanceServices = new WrappedServiceProvider(context.InstanceServices);

        return inner.CreateInstance(instanceType, context);
    }

    class WrappedServiceProvider(IServiceProvider inner) : IKeyedServiceProvider, ISupportRequiredService
    {
        readonly Binder parameterBinder = inner.GetRequiredService<Binder>();
        readonly IKeyedServiceProvider? keyedServiceProvider = inner as IKeyedServiceProvider;
        public object? GetKeyedService(Type serviceType, object? serviceKey)
        {
            Guard.IsNotNull(serviceType);

            var (key, isString) = Get(serviceKey);

            return isString ? GetKeyedService(serviceType, key) : keyedServiceProvider?.GetKeyedService(serviceType, serviceKey);
        }

        public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
        {
            Guard.IsNotNull(serviceType);

            var (key, isString) = Get(serviceKey);

            if (isString && GetKeyedService(serviceType, key) is object o)
            {
                return o;
            }

            return inner.GetRequiredKeyedService(serviceType, serviceKey);
        }

        public object? GetService(Type serviceType)
        {
            Guard.IsNotNull(serviceType);

            if (inner.GetService(serviceType) is object result)
            {
                return result;
            }

            return GetBindingService(serviceType, null);
        }

        public object GetRequiredService(Type serviceType)
        {
            Guard.IsNotNull(serviceType);

            if (inner.GetService(serviceType) is object result)
            {
                return result;
            }

            return GetBindingService(serviceType, null)
                ?? throw new InvalidOperationException($"Cannot create {serviceType.FullName} service.");
        }
        object? GetBindingService(Type serviceType, string? parameter)
        {
            if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                return parameterBinder.BindAsync(serviceType.GenericTypeArguments[0], parameter, default);
            }

            return parameterBinder.BindAsync(serviceType, parameter, default).GetAwaiter().GetResult();
        }
        object? GetKeyedService(Type serviceType, string? key)
        {
            if (keyedServiceProvider?.GetKeyedService(serviceType, key) is object result)
            {
                return result;
            }

            return GetBindingService(serviceType, key);
        }

        static (string? Key, bool IsStringKey) Get(object? serviceKey) =>
            serviceKey is string
                ? ((string?)serviceKey, true)
                : default;

    }
}

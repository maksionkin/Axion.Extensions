// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;

namespace Axion.Azure.Functions.Worker;

class Binder(IServiceProvider serviceProvider, IFunctionContextAccessor? functionContextAccessor = null) : IParameterBinder
{
    readonly IServiceProvider serviceProvider = functionContextAccessor?.FunctionContext?.InstanceServices ?? serviceProvider;
    public async ValueTask<T> BindAsync<T>(string? parameterName = null, CancellationToken cancellationToken = default) =>
        (T)(await BindAsync(typeof(T), parameterName, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Cannot bind {typeof(T).FullName} to parameter {parameterName}."));

    public async ValueTask<T> BindAsync<T>(CancellationToken cancellationToken = default)
    {
        var type = typeof(T);
        if (type.IsGenericType)
        {
            type = type.GenericTypeArguments[0];
        }

        return await BindAsync<T>(type.GetCustomAttribute<BindingAttribute>(), cancellationToken);
    }

    public async Task<T> BindAsync<T>(Attribute? attribute, CancellationToken cancellationToken = default) =>
        (T)(await BindAsync(typeof(T), attribute, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Cannot bind {typeof(T).FullName} to {attribute?.GetType().FullName}."));

    internal async Task<object?> BindAsync(Type type, string? parameterName, CancellationToken cancellationToken)
    {
        var context = functionContextAccessor?.FunctionContext;
        if (context != null)
        {
            var parameters = context.FunctionDefinition.Parameters;

            var parameter = parameterName == null
                ? parameters.SingleOrDefault(p => type == p.Type) ?? parameters.SingleOrDefault(p => type.IsAssignableFrom(p.Type))
                : parameters.SingleOrDefault(p => p.Name == parameterName);

            if (parameter != null)
            {
                var index = parameters.IndexOf(parameter);

                var attribute = context.GetOutputAttributes()[index];
                if (attribute != null)
                {
                    return await BindAsync(type, attribute, cancellationToken);
                }
            }
        }

        return null;
    }

    internal async Task<object?> BindAsync(Type type, Attribute? attribute, CancellationToken cancellationToken) =>
        attribute is BindingAttribute bindingAttribute
            ? await bindingAttribute.TryBindAsync(serviceProvider, type, cancellationToken).ConfigureAwait(false)
            : null;
}

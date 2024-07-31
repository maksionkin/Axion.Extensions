// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Axion.Extensions.DependencyInjection;

static class ExpressionHelpers
{
    static readonly NullabilityInfoContext nullabilityInfoContext = new();

    static readonly MethodInfo getServiceT = new Func<IServiceProvider, object?>(ServiceProviderServiceExtensions.GetService<object>).GetMethodInfo().GetGenericMethodDefinition();
    static readonly MethodInfo getRequiredServiceT = new Func<IServiceProvider, object>(ServiceProviderServiceExtensions.GetRequiredService<object>).GetMethodInfo().GetGenericMethodDefinition();
    public static Expression GetMethodCallExpression(MethodInfo method, ParameterExpression target, ParameterExpression provider, params Expression[] explicitExpressions)
    {
        var parameters = Array.ConvertAll(
             method.GetParameters(),
             parameter => GetServiceExpression(parameter, provider, explicitExpressions));

        var call = target == null
            ? Expression.Call(method, parameters)
            : Expression.Call(Expression.Convert(target, method.ReflectedType!), method, parameters);

        return method.ReturnType == typeof(void)
            ? call
            : Expression.Convert(call, typeof(object));
    }

    static Expression GetServiceExpression(ParameterInfo parameter, ParameterExpression provider, Expression[] explicitExpressions)
    {
        var parameters = new List<Expression>() { provider };
        MethodInfo baseMethod;
        var type = parameter.ParameterType;

        var notNullale = nullabilityInfoContext.Create(parameter).ReadState == NullabilityState.NotNull;

        if (explicitExpressions.FirstOrDefault(expression => parameter.ParameterType == expression.Type) is Expression expression)
        {
            return expression;
        }

        baseMethod = notNullale ? getRequiredServiceT : getServiceT;

        return Expression.Call(baseMethod.MakeGenericMethod(type), parameters);
    }
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Context.Features;

namespace Axion.Azure.Functions.Worker.Features;

class FunctionInputBindingFeature(IFunctionInputBindingFeature prev) : IFunctionInputBindingFeature
{
    public async ValueTask<FunctionInputBindingResult> BindFunctionInputAsync(FunctionContext context)
    {
        var result = await prev.BindFunctionInputAsync(context);
        var values = result.Values;

        if (values.Any(v => v == null))
        {
            var attributes = context.GetOutputAttributes();
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] == null && attributes[i] != null)
                {
                    values[i] = (await attributes[i]!.TryBindAsync(context.InstanceServices, context.FunctionDefinition.Parameters[i].Type, context.CancellationToken))
                        ?? throw new InvalidOperationException($"Cannot bind function input {context.FunctionDefinition.Parameters[i].Name} with [{attributes[i]!.GetType().FullName}].");
                }
            }
        }

        return result;
    }
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System.Threading.Tasks;
using Axion.Azure.Functions.Worker.Features;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Context.Features;
using Microsoft.Azure.Functions.Worker.Invocation;

namespace Axion.Azure.Functions.Worker;

class FunctionExecutor(IFunctionExecutor inner, IFunctionContextAccessor functionContextAccessor) : IFunctionExecutor
{
    public async ValueTask ExecuteAsync(FunctionContext context)
    {
        var previousFunctionInputBindingFeature = context.Features.Get<IFunctionInputBindingFeature>();
        var functionInputBindingFeatureReplaced = false;
        if (previousFunctionInputBindingFeature != null && previousFunctionInputBindingFeature is not FunctionInputBindingFeature)
        {
            context.Features.Set<IFunctionInputBindingFeature>(new FunctionInputBindingFeature(previousFunctionInputBindingFeature));

            functionInputBindingFeatureReplaced = true;
        }

        var previousContext = functionContextAccessor.FunctionContext;
        functionContextAccessor.FunctionContext = context;

        try
        {
            await inner.ExecuteAsync(context).ConfigureAwait(false);
        }
        finally
        {
            if (functionInputBindingFeatureReplaced)
            {
                context.Features.Set(previousFunctionInputBindingFeature!);
            }

            functionContextAccessor.FunctionContext = previousContext;
        }
    }
}

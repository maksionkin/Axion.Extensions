// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System.Threading;
using Microsoft.Azure.Functions.Worker;

namespace Axion.Azure.Functions.Worker;

class FunctionContextAccessor : IFunctionContextAccessor
{
    static readonly AsyncLocal<Wrapper> context = new();

    public virtual FunctionContext? FunctionContext
    {
        get
        {
            return context.Value?.Context;
        }
        set
        {
            var holder = context.Value;
            holder?.Context = null;

            if (value != null)
            {
                context.Value = new Wrapper { Context = value };
            }
        }
    }

    class Wrapper
    {
        public FunctionContext? Context;
    }
}

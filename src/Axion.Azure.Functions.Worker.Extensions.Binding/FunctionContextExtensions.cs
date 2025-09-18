// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Axion.Azure.Functions.Worker;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Azure.Functions.Worker;
#pragma warning restore IDE0130 // Namespace does not match folder structure

static class FunctionContextExtensions
{
    static readonly ConcurrentDictionary<Function, ImmutableArray<BindingAttribute?>> entryPoints = new();

    public static ImmutableArray<BindingAttribute?> GetOutputAttributes(this FunctionContext context) =>
        entryPoints.GetOrAdd(new Function(context), FetchAttributes);

    static ImmutableArray<BindingAttribute?> FetchAttributes(Function info)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.Location == info.PathToAssembly);

        var lastDelimiter = info.EntryPoint.LastIndexOf(Type.Delimiter);
        if (lastDelimiter < 0)
        {
            throw new InvalidOperationException($"Function EntryPoint type cannot be resolved.");
        }

        var type = assembly.GetType(info.EntryPoint[..lastDelimiter], true)!;
        var methodName = info.EntryPoint[(lastDelimiter + 1)..];

        var method = type.GetMethods()
            .FirstOrDefault(m => m.Name == methodName && m.GetCustomAttributes<FunctionAttribute>().Any(a => a.Name == info.FunctionName))
            ?? throw new InvalidOperationException($"Function EntryPoint type cannot be resolved.");

        return method.GetParameters()
            .Select(p => p.GetCustomAttribute<BindingAttribute>(true) ?? p.ParameterType.GetCustomAttribute<BindingAttribute>(true))
            .ToImmutableArray()!;
    }

    record struct Function(string PathToAssembly, string EntryPoint, string FunctionName)
    {
        public Function(FunctionContext context)
            : this(context.FunctionDefinition.PathToAssembly, context.FunctionDefinition.EntryPoint, context.FunctionDefinition.Name)
        {
        }
    }
}

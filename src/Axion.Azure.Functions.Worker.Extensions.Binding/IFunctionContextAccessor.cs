// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using Microsoft.Azure.Functions.Worker;

namespace Axion.Azure.Functions.Worker;

/// <summary>
/// Provides access to the current <see cref="FunctionContext"/> for the executing function.
/// </summary>
/// <remarks>This interface is typically used to retrieve or set the <see cref="FunctionContext"/>  within the
/// scope of a function execution. The <see cref="FunctionContext"/> contains  information about the current function
/// invocation, such as bindings, configuration,  and execution details.</remarks>
public interface IFunctionContextAccessor
{
    /// <summary>
    /// Gets or sets the current <see cref="FunctionContext"/> for the executing function.
    /// </summary>
    FunctionContext? FunctionContext { get; set; }
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker.Converters.Providers;

class SubTypeConverterProvider : IAsyncConverterProvider
{
    public object? GetAsyncConverter(Type input, Type output)
    {
        Guard.IsNotNull(input);
        Guard.IsNotNull(output);

        return output.IsAssignableFrom(input)
            ? Activator.CreateInstance(typeof(SubTypeAsyncConverter<,>).MakeGenericType(input, output))
            : null;
    }

    class SubTypeAsyncConverter<TInput, TOutput> : IAsyncConverter<TInput, TOutput>, IConverter<TInput, TOutput>
        where TInput : TOutput
    {
        public TOutput Convert(TInput input) =>
            input;

        public Task<TOutput> ConvertAsync(TInput input, CancellationToken cancellationToken) =>
            Task.FromResult(Convert(input));
    }
}

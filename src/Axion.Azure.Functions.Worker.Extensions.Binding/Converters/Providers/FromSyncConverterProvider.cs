// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker.Converters.Providers;

class FromSyncConverterProvider(IServiceProvider serviceProvider) : IAsyncConverterProvider
{
    public object? GetAsyncConverter(Type input, Type output)
    {
        Guard.IsNotNull(input);
        Guard.IsNotNull(output);

        return serviceProvider.GetService(typeof(IConverter<,>).MakeGenericType(input, output)) is object converter
            ? Activator.CreateInstance(
                typeof(SynchronousToAsynchronousConverter<,>).MakeGenericType(input, output),
                converter)
            : null;
    }

    class SynchronousToAsynchronousConverter<TInput, TOutput>(IConverter<TInput, TOutput> converter)
        : IAsyncConverter<TInput, TOutput>
    {
        public Task<TOutput> ConvertAsync(TInput input, CancellationToken cancellationToken)
        {
            var result = converter.Convert(input);

            return Task.FromResult(result);
        }
    }
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker.Converters.Providers;

class SyncConverterProvider(IServiceProvider serviceProvider) : IAsyncConverterProvider
{
    public object? GetAsyncConverter(Type input, Type output)
    {
        Guard.IsNotNull(input);
        Guard.IsNotNull(output);

        var converter = serviceProvider.GetService(typeof(IConverter<,>).MakeGenericType(input, output));

        if (converter != null)
        {
            return Activator.CreateInstance(
                typeof(AsyncFromSynchronousConverter<,>).MakeGenericType(input, output),
                converter);
        }

        return null;
    }

    class AsyncFromSynchronousConverter<TInput, TOutput>(IConverter<TInput, TOutput> converter)
        : IAsyncConverter<TInput, TOutput>
    {
        public Task<TOutput> ConvertAsync(TInput input, CancellationToken cancellationToken) =>
            Task.FromResult(converter.Convert(input));
    }
}

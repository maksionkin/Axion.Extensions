// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker.Converters.Providers;

class SameTypeConverterProvider : IAsyncConverterProvider
{
    public object? GetAsyncConverter(Type input, Type output) =>
        input == output
            ? Activator.CreateInstance(typeof(SameTypeAsyncConverter<>).MakeGenericType(input))
            : null;

    class SameTypeAsyncConverter<T> : IAsyncConverter<T, T>, IConverter<T, T>
    {
        public T Convert(T input) =>
            input;

        public Task<T> ConvertAsync(T input, CancellationToken cancellationToken) =>
            Task.FromResult(input);
    }
}

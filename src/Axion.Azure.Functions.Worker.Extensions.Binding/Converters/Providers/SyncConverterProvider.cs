// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker.Converters.Providers;

class AsyncConverterProvider(IServiceProvider serviceProvider) : IAsyncConverterProvider
{
    public object? GetAsyncConverter(Type input, Type output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        return serviceProvider.GetService(typeof(IAsyncConverter<,>).MakeGenericType(input, output));
    }
}

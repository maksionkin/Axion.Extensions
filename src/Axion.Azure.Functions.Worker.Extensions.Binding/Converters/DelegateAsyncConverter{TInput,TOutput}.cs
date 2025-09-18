// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker.Converters;

class DelegateAsyncConverter<TInput, TOutput>(Func<TInput, CancellationToken, ValueTask<TOutput>> convert) : IAsyncConverter<TInput, TOutput>
{
    public async Task<TOutput> ConvertAsync(TInput input, CancellationToken cancellationToken) =>
        await convert(input, cancellationToken).ConfigureAwait(false);
}

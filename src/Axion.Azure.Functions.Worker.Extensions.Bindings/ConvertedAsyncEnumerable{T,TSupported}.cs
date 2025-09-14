// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker;

class ConvertedAsyncEnumerable<T, TSupported>(IAsyncConverter<TSupported, T> converter, IAsyncEnumerable<TSupported> asyncEnumerable)
    : WrappedDisposable(asyncEnumerable), IAsyncEnumerable<T>, IEnumerable<T>
{
    public ConvertedAsyncEnumerable(IAsyncConverter<TSupported, T> converter, IEnumerable<TSupported> enumerable)
        : this(converter, enumerable.ToAsyncEnumerable())
    {
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var item in asyncEnumerable.WithCancellation(cancellationToken))
        {
            yield return converter.ConvertAsync(item, cancellationToken).GetAwaiter().GetResult();
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        var enumerator = GetAsyncEnumerator();
        while (enumerator.MoveNextAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult())
        {
            yield return enumerator.Current;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

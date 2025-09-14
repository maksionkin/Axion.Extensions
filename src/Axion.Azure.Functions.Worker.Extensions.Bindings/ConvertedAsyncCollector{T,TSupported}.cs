// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker;

class ConvertedAsyncCollector<T, TSupported>(IAsyncConverter<T, TSupported> converter, IAsyncCollector<TSupported> asyncCollector)
    : WrappedDisposable(asyncCollector), IAsyncCollector<T>, ICollector<T>
{
    public ConvertedAsyncCollector(IAsyncConverter<T, TSupported> converter, ICollector<TSupported> asyncCollector)
        : this(converter, new AsyncCollector<TSupported>(asyncCollector))
    {
    }

    public async Task AddAsync(T item, CancellationToken cancellationToken = default) =>
        await asyncCollector.AddAsync(await converter.ConvertAsync(item, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    public async Task FlushAsync(CancellationToken cancellationToken = default) =>
        await asyncCollector.FlushAsync(cancellationToken);

    public void Add(T item) =>
        AddAsync(item).GetAwaiter().GetResult();
}


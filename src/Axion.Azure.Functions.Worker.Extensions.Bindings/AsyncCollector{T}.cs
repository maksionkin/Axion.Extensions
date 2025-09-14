// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker;

class AsyncCollector<T>(ICollector<T> collector) : WrappedDisposable(collector), IAsyncCollector<T>
{
    public Task AddAsync(T item, CancellationToken cancellationToken = default)
    {
        collector.Add(item);

        return Task.CompletedTask;
    }

    public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

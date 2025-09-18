// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker;

class Collector<T>(IAsyncCollector<T> collector) : WrappedDisposable(collector), ICollector<T>
{
    public void Add(T item)
    {
        async ValueTask AddAndFlushAsync()
        {
            await collector.AddAsync(item);
            await collector.FlushAsync();
        }

        AddAndFlushAsync().AsTask().GetAwaiter().GetResult();
    }
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker;

class BinaryDataAsyncCollector(Stream stream) : IAsyncCollector<BinaryData>, IDisposable, IAsyncDisposable
{
    public async Task AddAsync(BinaryData item, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(item);

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        await stream.WriteAsync(item.ToMemory(), cancellationToken);
#else
        var array = item.ToArray();
        await stream.WriteAsync(array, 0, array.Length, cancellationToken);
#endif
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default) =>
        await stream.FlushAsync(cancellationToken);

    protected virtual void Dispose(bool disposing)
    {

        if (disposing)
        {
            stream.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (stream is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
        else
        {
            Dispose(disposing: true);
        }
    }
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Threading.Tasks;

namespace Axion.Azure.Functions.Worker;

abstract class WrappedDisposable(object owned) : IDisposable, IAsyncDisposable
{
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            (owned as IDisposable)?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    public async ValueTask DisposeAsync()
    {
        if (owned is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
        else
        {
            Dispose();
        }
    }
}

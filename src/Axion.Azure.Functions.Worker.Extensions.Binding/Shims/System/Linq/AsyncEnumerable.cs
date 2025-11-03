// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Linq;
#pragma warning restore IDE0130 // Namespace does not match folder structure

#if !NET7_0_OR_GREATER
static class AsyncEnumerable
{
    public static IEnumerable<T> ToBlockingEnumerable<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(source);

        var e = source.GetAsyncEnumerator(cancellationToken);

        try
        {
            while (e.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                yield return e.Current;
            }
        }
        finally
        {
            e.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
#endif

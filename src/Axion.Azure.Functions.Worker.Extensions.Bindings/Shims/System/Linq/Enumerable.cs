// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System.Collections.Generic;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Linq;
#pragma warning restore IDE0130 // Namespace does not match folder structure

#if !NETCOREAPP1_0_OR_GREATER && !NETSTANDARD1_6_OR_GREATER && !NET471_OR_GREATER
static class Enumerable
{
    public static IEnumerable<TSource> Prepend<TSource>(this IEnumerable<TSource> source, TSource element)
    {
        yield return element;

        foreach (var item in source)
        {
            yield return item;
        }
    }

}

#endif

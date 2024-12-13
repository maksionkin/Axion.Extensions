// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;

namespace Axion.Extensions.Caching.Hybrid.Serialization.Http;

class SettableReadOnlySequenceSegment<T> : ReadOnlySequenceSegment<T>
{
    public SettableReadOnlySequenceSegment<T> SetNext(ReadOnlySequenceSegment<T> next)
    {
        Next = next;

        return this;
    }

    public SettableReadOnlySequenceSegment<T> SetMemory(ReadOnlyMemory<T> memory)
    {
        Memory = memory;

        return this;
    }

    public SettableReadOnlySequenceSegment<T> SetRunningIndex(long runningIndex)
    {
        RunningIndex = runningIndex;

        return this;
    }
}


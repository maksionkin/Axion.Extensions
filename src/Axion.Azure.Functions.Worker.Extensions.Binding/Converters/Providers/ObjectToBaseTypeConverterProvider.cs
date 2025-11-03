// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using CommunityToolkit.Diagnostics;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker.Converters.Providers;

class ObjectToBaseTypeConverterProvider(IServiceProvider serviceProvider) : IAsyncConverterProvider
{
    static readonly ImmutableArray<Type> BaseTypes =
    [
        typeof(BinaryData),
        typeof(Stream),
        typeof(byte[]),
        typeof(ReadOnlyMemory<byte>),
        typeof(string),
    ];

    public object? GetAsyncConverter(Type input, Type output)
    {
        Guard.IsNotNull(input);
        Guard.IsNotNull(output);

        if (BaseTypes.Contains(output))
        {
            var objectSerializer = serviceProvider.GetObjectSerializer();

            return Activator.CreateInstance(typeof(ObjectToBaseTypeAsyncConverter<>).MakeGenericType(input), objectSerializer)!;
        }

        return null;
    }

    class ObjectToBaseTypeAsyncConverter<T>(ObjectSerializer objectSerializer) :
        IAsyncConverter<T, BinaryData>,
        IAsyncConverter<T, Stream>,
        IAsyncConverter<T, byte[]>,
        IAsyncConverter<T, ReadOnlyMemory<byte>>,
        IAsyncConverter<T, string>,
        IConverter<T, BinaryData>,
        IConverter<T, Stream>,
        IConverter<T, byte[]>,
        IConverter<T, ReadOnlyMemory<byte>>,
        IConverter<T, string>
    {
        public BinaryData Convert(T input) =>
            objectSerializer.Serialize(input, typeof(T));

        public Task<Stream> ConvertAsync(T input, CancellationToken cancellationToken) =>
            Task.FromResult(Convert(input).ToStream());

        Stream IConverter<T, Stream>.Convert(T input) =>
            Convert(input).ToStream();

        byte[] IConverter<T, byte[]>.Convert(T input) =>
            Convert(input).ToArray();

        ReadOnlyMemory<byte> IConverter<T, ReadOnlyMemory<byte>>.Convert(T input) =>
            Convert(input).ToMemory();

        string IConverter<T, string>.Convert(T input) =>
            Convert(input).ToString();

        Task<BinaryData> IAsyncConverter<T, BinaryData>.ConvertAsync(T input, CancellationToken cancellationToken) =>
            Task.FromResult(Convert(input));

        Task<byte[]> IAsyncConverter<T, byte[]>.ConvertAsync(T input, CancellationToken cancellationToken) =>
            Task.FromResult(Convert(input).ToArray());

        Task<ReadOnlyMemory<byte>> IAsyncConverter<T, ReadOnlyMemory<byte>>.ConvertAsync(T input, CancellationToken cancellationToken) =>
            Task.FromResult(Convert(input).ToMemory());

        Task<string> IAsyncConverter<T, string>.ConvertAsync(T input, CancellationToken cancellationToken) =>
            Task.FromResult(Convert(input).ToString());
    }
}

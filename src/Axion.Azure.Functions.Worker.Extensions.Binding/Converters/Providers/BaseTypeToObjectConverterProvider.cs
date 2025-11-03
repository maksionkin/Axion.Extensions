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

class BaseTypeToObjectConverterProvider(IServiceProvider serviceProvider) : IAsyncConverterProvider
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

        if (BaseTypes.Contains(input))
        {
            var objectSerializer = serviceProvider.GetObjectSerializer();

            return Activator.CreateInstance(typeof(BaseTypeToObjectAsyncConverter<>).MakeGenericType(input), objectSerializer)!;
        }

        return null;
    }

    class BaseTypeToObjectAsyncConverter<T>(ObjectSerializer objectSerializer) :
        IAsyncConverter<BinaryData, T>,
        IAsyncConverter<Stream, T>,
        IAsyncConverter<byte[], T>,
        IAsyncConverter<ReadOnlyMemory<byte>, T>,
        IAsyncConverter<string, T>
    {
        public async Task<T> ConvertAsync(Stream input, CancellationToken cancellationToken)
        {
            Guard.IsNotNull(input);

            return (T)(await objectSerializer.DeserializeAsync(input, typeof(T), cancellationToken))!;
        }

        public async Task<T> ConvertAsync(BinaryData input, CancellationToken cancellationToken)
        {
            Guard.IsNotNull(input);

            return await ConvertAsync(input.ToStream(), cancellationToken);
        }

        public async Task<T> ConvertAsync(byte[] input, CancellationToken cancellationToken)
        {
            Guard.IsNotNull(input);

            return await ConvertAsync(BinaryData.FromBytes(input), cancellationToken);
        }

        public async Task<T> ConvertAsync(string input, CancellationToken cancellationToken)
        {
            Guard.IsNotNull(input);

            return await ConvertAsync(BinaryData.FromString(input), cancellationToken);
        }

        public async Task<T> ConvertAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken)
        {
            Guard.IsNotNull(input);

            return await ConvertAsync(BinaryData.FromBytes(input), cancellationToken);
        }
    }
}

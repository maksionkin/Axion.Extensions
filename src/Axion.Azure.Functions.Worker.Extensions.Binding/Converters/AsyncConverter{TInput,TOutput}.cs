// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using CommunityToolkit.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Axion.Azure.Functions.Worker.Converters;

class AsyncConverter<TInput, TOutput>(IServiceProvider serviceProvider) : IAsyncConverter<TInput, TOutput>
{
    static readonly ImmutableArray<Type> BaseTypes = [typeof(string), typeof(BinaryData), typeof(byte[]), typeof(Stream), typeof(ReadOnlyMemory<byte>)];

    readonly Func<TInput, CancellationToken, ValueTask<TOutput>> convert = GetConvertFunc(serviceProvider);

    static Func<TInput, CancellationToken, ValueTask<TOutput>> GetConvertFunc(IServiceProvider serviceProvider)
    {
        Guard.IsNotNull(serviceProvider);

        var converter = serviceProvider.GetService<IConverter<TInput, TOutput>>();
        if (converter != null)
        {
            return (input, _) => new(converter.Convert(input));
        }
        else if (typeof(TInput) == typeof(TOutput) || typeof(TOutput).IsAssignableFrom(typeof(TInput)))
        {
            return (input, _) => new((TOutput)(object)input);
        }
        else if (BaseTypes.Contains(typeof(TInput)) ^ BaseTypes.Contains(typeof(TOutput)))
        {
            var serializer = serviceProvider.GetService<IOptionsMonitor<WorkerOptions>>()?.CurrentValue.Serializer;
            if (serializer == null)
            {
                var jsonSerializerOptions = serviceProvider.GetService<IOptionsMonitor<JsonSerializerOptions>>()?.CurrentValue;

                serializer = jsonSerializerOptions == null
                    ? JsonObjectSerializer.Default
                    : new JsonObjectSerializer(jsonSerializerOptions);
            }

            if (BaseTypes.Contains(typeof(TOutput)))
            {
                async ValueTask<BinaryData> ConvertToBinaryDataAsync(TInput input, CancellationToken cancellationToken) =>
                     await serializer.SerializeAsync(input, typeof(TInput), cancellationToken).ConfigureAwait(false);

                if (typeof(TOutput) == typeof(BinaryData))
                {
                    return (Func<TInput, CancellationToken, ValueTask<TOutput>>)(object)ConvertToBinaryDataAsync;
                }

                var fromBinaryData = serviceProvider.GetRequiredService<IAsyncConverter<BinaryData, TOutput>>();

                return async (input, cancellationToken) => await fromBinaryData.ConvertAsync(await ConvertToBinaryDataAsync(input, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                async ValueTask<TOutput?> ConvertFromStreamAsync(Stream input, CancellationToken cancellationToken)
                {
                    Guard.IsNotNull(input);

                    var result = await serializer.DeserializeAsync(input, typeof(TInput), cancellationToken).ConfigureAwait(false);

                    return (TOutput?)result;
                }

                if (typeof(TInput) == typeof(Stream))
                {
                    return (Func<TInput, CancellationToken, ValueTask<TOutput>>)(object)ConvertFromStreamAsync;
                }
                else
                {
                    var toStream = serviceProvider.GetRequiredService<IAsyncConverter<TInput, Stream>>();

                    return async (input, cancellationToken) => await ConvertFromStreamAsync(await toStream.ConvertAsync(input, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        throw new NotSupportedException($"Conversion from {typeof(TInput).FullName} to {typeof(TOutput).FullName} is not supported.");
    }

    public async Task<TOutput> ConvertAsync(TInput input, CancellationToken cancellationToken) =>
        await convert(input, cancellationToken);
}

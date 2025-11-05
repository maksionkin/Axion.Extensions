// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker.Converters.Providers;

class EnumerationConverterProvider(IServiceProvider serviceProvider) : IAsyncConverterProvider
{
    static readonly ImmutableArray<Type> BaseTypes =
    [
        typeof(IAsyncEnumerable<>),
        typeof(IEnumerable<>),
    ];

    public object? GetAsyncConverter(Type input, Type output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        static Type? GetItemType(Type type) =>
            type switch
            {
                { IsGenericType: true } when BaseTypes.Contains(type.GetGenericTypeDefinition()) => type.GetGenericArguments()[0],
                { IsArray: true } when type.GetArrayRank() == 1 => type.GetElementType(),
                _ => null,
            };

        if (GetItemType(input) is Type inputItemType && GetItemType(output) is Type outputItemType)
        {
            var converter = serviceProvider.GetAsyncConverter(inputItemType, outputItemType, GetType().IsInstanceOfType);
            if (converter != null)
            {
                var converterType = typeof(EnumerationConverter<,>).MakeGenericType(inputItemType, outputItemType);

                return Activator.CreateInstance(converterType, converter);
            }
        }

        return null;
    }

    class EnumerationConverter<TInputItem, TOutputItem>(IAsyncConverter<TInputItem, TOutputItem> itemConverter) :
        IAsyncConverter<IAsyncEnumerable<TInputItem>, IAsyncEnumerable<TOutputItem>>,
        IAsyncConverter<IAsyncEnumerable<TInputItem>, IEnumerable<TOutputItem>>,
        IAsyncConverter<IEnumerable<TInputItem>, IAsyncEnumerable<TOutputItem>>,
        IAsyncConverter<IEnumerable<TInputItem>, IEnumerable<TOutputItem>>,
        IAsyncConverter<TInputItem[], TOutputItem[]>,
        IAsyncConverter<IAsyncEnumerable<TInputItem>, TOutputItem[]>,
        IAsyncConverter<IEnumerable<TInputItem>, TOutputItem[]>,
        IAsyncConverter<TInputItem[], IAsyncEnumerable<TOutputItem>>,
        IAsyncConverter<TInputItem[], IEnumerable<TOutputItem>>

    {
        public IAsyncEnumerable<TOutputItem> Convert(IAsyncEnumerable<TInputItem>? input, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(input);

            return input.Select(async (TInputItem item, CancellationToken token) => await itemConverter.ConvertAsync(item, token).ConfigureAwait(false));
        }

        public Task<IAsyncEnumerable<TOutputItem>> ConvertAsync(TInputItem[] input, CancellationToken cancellationToken) =>
            Task.FromResult(Convert(input.ToAsyncEnumerable(), cancellationToken));

        Task<IAsyncEnumerable<TOutputItem>> IAsyncConverter<IAsyncEnumerable<TInputItem>, IAsyncEnumerable<TOutputItem>>.ConvertAsync(IAsyncEnumerable<TInputItem> input, CancellationToken cancellationToken) =>
            Task.FromResult(Convert(input, cancellationToken));

        Task<IEnumerable<TOutputItem>> IAsyncConverter<IAsyncEnumerable<TInputItem>, IEnumerable<TOutputItem>>.ConvertAsync(IAsyncEnumerable<TInputItem> input, CancellationToken cancellationToken) =>
            Task.FromResult(Convert(input, cancellationToken).ToBlockingEnumerable(cancellationToken));

        Task<IAsyncEnumerable<TOutputItem>> IAsyncConverter<IEnumerable<TInputItem>, IAsyncEnumerable<TOutputItem>>.ConvertAsync(IEnumerable<TInputItem> input, CancellationToken cancellationToken) =>
            Task.FromResult(Convert(input?.ToAsyncEnumerable(), cancellationToken));

        Task<IEnumerable<TOutputItem>> IAsyncConverter<IEnumerable<TInputItem>, IEnumerable<TOutputItem>>.ConvertAsync(IEnumerable<TInputItem> input, CancellationToken cancellationToken) =>
            Task.FromResult(Convert(input?.ToAsyncEnumerable(), cancellationToken).ToBlockingEnumerable(cancellationToken));

        async Task<TOutputItem[]> IAsyncConverter<TInputItem[], TOutputItem[]>.ConvertAsync(TInputItem[] input, CancellationToken cancellationToken) =>
            await Convert(input?.ToAsyncEnumerable(), cancellationToken).ToArrayAsync(cancellationToken).ConfigureAwait(false);

        async Task<TOutputItem[]> IAsyncConverter<IAsyncEnumerable<TInputItem>, TOutputItem[]>.ConvertAsync(IAsyncEnumerable<TInputItem> input, CancellationToken cancellationToken) =>
            await Convert(input, cancellationToken).ToArrayAsync(cancellationToken).ConfigureAwait(false);


        async Task<TOutputItem[]> IAsyncConverter<IEnumerable<TInputItem>, TOutputItem[]>.ConvertAsync(IEnumerable<TInputItem> input, CancellationToken cancellationToken) =>
            await Convert(input?.ToAsyncEnumerable(), cancellationToken).ToArrayAsync(cancellationToken).ConfigureAwait(false);

        Task<IEnumerable<TOutputItem>> IAsyncConverter<TInputItem[], IEnumerable<TOutputItem>>.ConvertAsync(TInputItem[] input, CancellationToken cancellationToken) =>
            Task.FromResult(Convert(input?.ToAsyncEnumerable(), cancellationToken).ToBlockingEnumerable(cancellationToken));
    }
}

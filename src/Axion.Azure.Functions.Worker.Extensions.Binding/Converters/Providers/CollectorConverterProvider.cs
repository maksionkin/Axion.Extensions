// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker.Converters.Providers;

class CollectorConverterProvider(IServiceProvider serviceProvider) : IAsyncConverterProvider
{
    static readonly ImmutableArray<Type> BaseTypes =
    [
        typeof(IAsyncCollector<>),
        typeof(ICollector<>),
    ];

    public object? GetAsyncConverter(Type input, Type output)
    {
        Guard.IsNotNull(input);
        Guard.IsNotNull(output);

        if (input.IsGenericType && output.IsGenericType)
        {
            var inputDefinition = input.GetGenericTypeDefinition();
            var outputDefinition = output.GetGenericTypeDefinition();

            if (BaseTypes.Contains(inputDefinition) && BaseTypes.Contains(outputDefinition))
            {
                var inputItemType = input.GetGenericArguments()[0];
                var outputItemType = output.GetGenericArguments()[0];

                var converter = serviceProvider.GetAsyncConverter(outputItemType, inputItemType, GetType().IsInstanceOfType);

                if (converter != null)
                {
                    var converterType = typeof(CollectorConverter<,>).MakeGenericType(inputItemType, outputItemType);

                    return Activator.CreateInstance(converterType, converter);
                }
            }
        }

        return null;
    }

    class CollectorConverter<TInputItem, TOutputItem>(IAsyncConverter<TOutputItem, TInputItem> itemConverter) :
        IAsyncConverter<IAsyncCollector<TInputItem>, IAsyncCollector<TOutputItem>>,
        IAsyncConverter<IAsyncCollector<TInputItem>, ICollector<TOutputItem>>,
        IAsyncConverter<ICollector<TInputItem>, IAsyncCollector<TOutputItem>>,
        IAsyncConverter<ICollector<TInputItem>, ICollector<TOutputItem>>
    {
        public AsyncCollectorWrapper<TInputItem, TOutputItem> Convert(IAsyncCollector<TInputItem> input)
        {
            Guard.IsNotNull(input);

            return new AsyncCollectorWrapper<TInputItem, TOutputItem>(input, itemConverter);
        }

        public Task<ICollector<TOutputItem>> ConvertAsync(IAsyncCollector<TInputItem> input, CancellationToken cancellationToken) =>
            Task.FromResult<ICollector<TOutputItem>>(Convert(input));

        Task<IAsyncCollector<TOutputItem>> IAsyncConverter<IAsyncCollector<TInputItem>, IAsyncCollector<TOutputItem>>.ConvertAsync(IAsyncCollector<TInputItem> input, CancellationToken cancellationToken) =>
            Task.FromResult<IAsyncCollector<TOutputItem>>(Convert(input));

        Task<IAsyncCollector<TOutputItem>> IAsyncConverter<ICollector<TInputItem>, IAsyncCollector<TOutputItem>>.ConvertAsync(ICollector<TInputItem> input, CancellationToken cancellationToken) =>
            Task.FromResult<IAsyncCollector<TOutputItem>>(Convert(input.ToAsyncCollector()));

        Task<ICollector<TOutputItem>> IAsyncConverter<ICollector<TInputItem>, ICollector<TOutputItem>>.ConvertAsync(ICollector<TInputItem> input, CancellationToken cancellationToken) =>
            Task.FromResult<ICollector<TOutputItem>>(Convert(input.ToAsyncCollector()));
    }

    class AsyncCollectorWrapper<TInputItem, TOutputItem>(IAsyncCollector<TInputItem> inner, IAsyncConverter<TOutputItem, TInputItem> itemConverter) :
        IAsyncCollector<TOutputItem>,
        ICollector<TOutputItem>
    {
        public void Add(TOutputItem item)
        {
            async ValueTask AddAndFlushAsync()
            {
                var convertedItem = await itemConverter.ConvertAsync(item, CancellationToken.None);

                await inner.AddAsync(convertedItem, CancellationToken.None);
                await inner.FlushAsync(CancellationToken.None);
            }

            AddAndFlushAsync().AsTask().GetAwaiter().GetResult();
        }

        public async Task AddAsync(TOutputItem item, CancellationToken cancellationToken = default)
        {
            var convertedItem = await itemConverter.ConvertAsync(item, cancellationToken);

            await inner.AddAsync(convertedItem, cancellationToken);
        }
        public Task FlushAsync(CancellationToken cancellationToken = default) =>
            inner.FlushAsync(cancellationToken);
    }


}

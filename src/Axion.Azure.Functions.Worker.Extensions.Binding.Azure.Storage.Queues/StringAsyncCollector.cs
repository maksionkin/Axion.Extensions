// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using CommunityToolkit.Diagnostics;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker;

class StringAsyncCollector(QueueClient queueClient, bool createIfNotExist) : IAsyncCollector<string>
{
    public async Task AddAsync(string item, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(item);

        while (true)
        {
            try
            {
                await queueClient.SendMessageAsync(
                    messageText: item,
                    cancellationToken: cancellationToken);

                return;
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == QueueErrorCode.QueueNotFound && createIfNotExist)
            {
                await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }
        }
    }

    public Task FlushAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

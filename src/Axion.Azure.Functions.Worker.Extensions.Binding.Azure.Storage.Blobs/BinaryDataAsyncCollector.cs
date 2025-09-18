// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Specialized;
using CommunityToolkit.Diagnostics;
using Microsoft.Azure.WebJobs;

namespace Axion.Azure.Functions.Worker;

class BinaryDataAsyncCollector(AppendBlobClient blobClient) : IAsyncCollector<BinaryData>
{
    public async Task AddAsync(BinaryData item, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(item);

        while(true)
        {
            try
            {
                await blobClient.AppendBlockAsync(item.ToStream(), cancellationToken: cancellationToken);

                break;
            }
            catch (RequestFailedException rfe) when (rfe.Status == (int)HttpStatusCode.NotFound)
            {
                await blobClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }
        }
    }

    public Task FlushAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

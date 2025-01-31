using System.Net.Http;
using Axion.Extensions.Caching.Hybrid.Serialization.Http;
using Azure.Storage.Blobs;
using Medallion.Threading;
using Medallion.Threading.Azure;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Polly;

namespace Axion.Extensions.Http.Resilience.DuplicateRequestCollapsing.Caching.Hybrid.Tests;

[TestClass]
public class DuplicateRequestCollapserTests
{
    static ServiceProvider GetServices()
    {
        var guid = Guid.NewGuid();

        var services = new ServiceCollection();

        services.AddAzureClients(builder => builder.AddBlobServiceClient("UseDevelopmentStorage=true"));

        services.AddAzureBlobCache();

        services.AddTransient<DelayedHttpHandler>();

        services.AddSingleton<IDistributedLockProvider>(provider => { var containerClient = provider.GetRequiredService<BlobServiceClient>().GetBlobContainerClient("locks"); containerClient.CreateIfNotExists(); return new AzureBlobLeaseDistributedSynchronizationProvider(containerClient); });

        var httpClientBuilder = services.AddHttpClient("test");
        httpClientBuilder.AddResilienceHandler(
            "test",
            (pipeline, handlerContext) =>
            {
                var options = new HttpDuplicateRequestCollapsingStrategyOptions()
                {
                    HybridCache = handlerContext.ServiceProvider.GetRequiredService<HybridCache>(),
                    LockHandleProvider = async (context, key) => await handlerContext.ServiceProvider.GetRequiredService<IDistributedLockProvider>().AcquireLockAsync(key, cancellationToken: context.CancellationToken),
                };

                var prev = options.CacheKeyProvider;
                options.CacheKeyProvider = async c => await prev(c) + $"-{guid}";

                pipeline.AddDuplicateRequestCollapsing(options);
            });

        httpClientBuilder.AddHttpMessageHandler<DelayedHttpHandler>();

#pragma warning disable EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        services.AddHybridCache(op => { }).AddSerializer(HttpResponseMessageHybridCacheSerializer.Instance);
#pragma warning restore EXTEXP0018 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        return services.BuildServiceProvider();
    }

    [TestMethod]
    public async Task OnlyOneCalled_FreshCache()
    {
        var provider = GetServices();
        var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");


        var called = new bool[6];

        async Task<HttpResponseMessage> Call(int i)
        {
            called[i] = true;

            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/");

            return await httpClient.SendAsync(request);
        }

        var tasks = called.Select((_, i) => Call(i));

        var results = await Task.WhenAll(tasks);

        var data = results.Select(message => (message.Headers.GetValues("x-id").First(), message.Content.ReadAsStringAsync().Result)).ToList();

        Assert.AreEqual(1, data.Distinct().Count());
    }

    class DelayedHttpHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(600, cancellationToken);

            var response = await base.SendAsync(request, cancellationToken);

            response.Headers.Add("x-id", Guid.NewGuid().ToString());

            return response;
        }
    }
}

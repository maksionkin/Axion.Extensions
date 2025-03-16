using Azure.Storage.Blobs;
using Medallion.Threading;
using Medallion.Threading.Azure;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;

namespace Axion.Extensions.Polly.DuplicateRequestCollapsing.Caching.Hybrid.Tests;

[TestClass]
public class DuplicateRequestCollapserTests
{
    static ServiceProvider GetServices()
    {
        var services = new ServiceCollection();

        services.AddAzureClients(builder => builder.AddBlobServiceClient("UseDevelopmentStorage=true"));

        services.AddAzureBlobCache();

        services.AddSingleton<IDistributedLockProvider>(provider => { var containerClient = provider.GetRequiredService<BlobServiceClient>().GetBlobContainerClient("locks"); containerClient.CreateIfNotExists(); return new AzureBlobLeaseDistributedSynchronizationProvider(containerClient); });

        services.AddResiliencePipeline<string, Holder>(
            "test-pipe",
            (pipeline, add) =>
            pipeline.AddDuplicateRequestCollapsing(
                new()
                {
                    LockHandleProvider = async (context, key) => await add.ServiceProvider.GetRequiredService<IDistributedLockProvider>().AcquireLockAsync(key, cancellationToken: context.CancellationToken),

                    HybridCache = add.ServiceProvider.GetRequiredService<HybridCache>(),
                }));

        services.AddHybridCache(op => { });

        return services.BuildServiceProvider();
    }

    [TestMethod]
    public async Task OnlyOneCalled_FreshCache()
    {
        var provider = GetServices();
        var pipeline = provider.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline<Holder>("test-pipe");

        var key = $"test-{Guid.NewGuid()}";

        var called = new bool[6];

        async ValueTask<Holder> Call(ResilienceContext context, int i)
        {
            await Task.Delay(600, context.CancellationToken);

            called[i] = true;

            return new Holder(i + 1);
        }

        var pool = ResilienceContextPool.Shared;
        var context = pool.Get(key);
        var tasks = called.Select((_, i) => pipeline.ExecuteAsync(Call, context, i).AsTask());

        var results = await Task.WhenAll(tasks);

        Assert.AreEqual(1, called.Count(c => c));
        Assert.AreEqual(1, results.Distinct().Count());
    }

    [TestMethod]
    public async Task NoOneCalled_Cache()
    {
        var provider = GetServices();
        var cache = provider.GetRequiredService<HybridCache>();
        var pipeline = provider.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline<Holder>("test-pipe");

        var key = $"test-{Guid.NewGuid()}";

        var cached = new Holder(-1);

        await cache.SetAsync(key, cached);

        var called = new bool[13];

        async ValueTask<Holder> Call(ResilienceContext context, int i)
        {
            await Task.Delay(30000, context.CancellationToken);

            called[i] = true;

            return new Holder(i + 1);
        }

        var pool = ResilienceContextPool.Shared;
        var context = pool.Get(key);
        var tasks = called.Select((_, i) => pipeline.ExecuteAsync(Call, context, i).AsTask());

        var results = await Task.WhenAll(tasks);

        Assert.IsTrue(called.All(c => !c));
        Assert.IsTrue(results.All(c => cached == c));
    }

    record Holder(int Value);
}

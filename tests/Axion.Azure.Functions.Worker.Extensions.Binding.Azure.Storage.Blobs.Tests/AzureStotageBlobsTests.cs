using System.Reflection;
using System.Text;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Axion.Azure.Functions.Worker.Extensions.Binding.Azure.Storage.Blobs.Tests;

[TestClass]
public class AzureStotageBlobsTests
{
    static IServiceProvider GetProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new("AzureWebJobsStorage", "UseDevelopmentStorage=true")]);
        var services = new ServiceCollection();
        
        if (Assembly.GetEntryAssembly() == null)
        {
            services.AddSingleton<IFunctionsWorkerApplicationBuilder>(new FunctionsWorkerApplicationBuilder(services));
        }

        services.AddFunctionsWorkerCore();
        services.AddWorkerBinding();
        services.AddAzureBlobsConverters();

        services.AddSingleton<IConfiguration>(configuration.Build());

        services.AddTransient<Service1>();

        return services.BuildServiceProvider();
    }

    [TestMethod]
    public async Task CheckAsyncCollector()
    {
        // Arrange
        var provider = GetProvider();

        var service1 = provider.GetRequiredService<Service1>();
        var blobClient = await service1.GetBlobClientAsync();
        await blobClient.DeleteIfExistsAsync();

        var sb = new StringBuilder();

        // Act
        for (var i = 0; i < 6; i++)
        {
            await service1.RunAsync(i);
            sb.Append($"{{\"Content\":{i}}}");
        }

        // Assert
        var blobDownloadInfo = await blobClient.DownloadContentAsync();

        Assert.AreEqual(sb.ToString(), blobDownloadInfo.Value.Content.ToString());
    }

    class Service1(ITypeBinder binder)
    {
        IAsyncCollector<Poco> collector;

        public async Task RunAsync(int data)
        {

            collector ??= await binder.BindAsync<IAsyncCollector<Poco>>(CancellationToken.None);

            await collector.AddAsync(new() { Content = data});
        }

        public async Task<BlobClient> GetBlobClientAsync() =>
            await binder.BindAsync<BlobClient>(new BlobAttribute("container/my-blob"), CancellationToken.None);
    }

    [Blob("container/my-blob")]
    class Poco
    {
        public  int Content { get; set; }
    }
    class FunctionsWorkerApplicationBuilder(IServiceCollection services) : IFunctionsWorkerApplicationBuilder
    {
        public IServiceCollection Services => services;

        public IFunctionsWorkerApplicationBuilder Use(Func<FunctionExecutionDelegate, FunctionExecutionDelegate> middleware)
        {
            throw new NotImplementedException();
        }
    }
}

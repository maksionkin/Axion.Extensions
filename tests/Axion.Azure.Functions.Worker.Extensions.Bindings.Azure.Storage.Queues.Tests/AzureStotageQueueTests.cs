using System.Collections.Immutable;
using System.Reflection;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static System.Net.Mime.MediaTypeNames;

namespace Axion.Azure.Functions.Worker.Extensions.Bindings.Azure.Storage.Queues.Tests;

[TestClass]
public class AzureStotageQueueTests
{
    static IServiceProvider GetProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new("AzureWebJobsStorage", "UseDevelopmentStorage=true")]);
        var services = new ServiceCollection();

        services.AddFunctionsWorkerCore();
        services.AddWorkerBinding();

        services.AddSingleton<IConfiguration>(configuration.Build());

        services.AddTransient<Service1>()
            .AddTransient<Service2>();

        return services.BuildServiceProvider();
    }

    [TestMethod]
    public async Task CheckAsyncCollector()
    {         
        // Arrange
        var provider = GetProvider(); 
        var queueClient = new QueueClient("UseDevelopmentStorage=true", "my-queue-1", new QueueClientOptions() { MessageEncoding = QueueMessageEncoding.Base64 });
        await queueClient.DeleteIfExistsAsync();

        var data = BinaryData.FromString("test-message");

        // Act
        var service1 = provider.GetRequiredService<Service1>();
        
        // Assert
        await service1.RunAsync(data);
        Assert.IsTrue(await queueClient.ExistsAsync());
        var message = (await queueClient.ReceiveMessagesAsync(1)).Value.FirstOrDefault()?.Body;
        Assert.AreEqual(data.ToString(), message?.ToString());
    }

    [TestMethod]
    public async Task CheckAsyncCollectorPoco()
    {
        // Arrange
        var provider = GetProvider();
        var data = "test-message";

        // Act
        var service2 = provider.GetRequiredService<Service2>();
        var queueClient = await service2.GetQueueClientAsync();
        await queueClient.DeleteIfExistsAsync();

        // Assert
        service2.Run(data);
        Assert.IsTrue(await queueClient.ExistsAsync());
        var message = (await queueClient.ReceiveMessagesAsync(1)).Value.FirstOrDefault()?.Body;
        
        Assert.AreEqual($"{{\"Content\":\"{data}\"}}", message?.ToString());
    }

    class Service1(IBinder binder)
    {
        public async Task RunAsync(BinaryData data)
        {
            var attribute = new QueueAttribute("my-queue-1");

            var collector = await binder.BindAsync<IAsyncCollector<BinaryData>>(attribute, CancellationToken.None);
            await collector.AddAsync(data);
        }
    }

    class Service2(ITypeBinder typeBinder)
    {
        public void Run(string content)
        {
            var collector = typeBinder.BindAsync<ICollector<Poco>>().Result;

            collector.Add(new() { Content = content });
        }

        public async Task<QueueClient> GetQueueClientAsync()
        {
            var attribute = new QueueAttribute("my-queue-2");

            return await typeBinder.BindAsync<QueueClient>(attribute, CancellationToken.None);
        }
    }

    [Queue("my-queue-2")]
    class Poco
    {
        public required string Content { get; set; }
    }
}

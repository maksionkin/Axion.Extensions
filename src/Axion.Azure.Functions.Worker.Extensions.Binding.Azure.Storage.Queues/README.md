## Axion.Azure.Functions.Worker.Extensions.Binding.Azure.Storage.Queues

Support for [bindings](./src/Axion.Azure.Functions.Worker.Extensions.Binding) [![NuGet version](https://badge.fury.io/nu/Axion.Azure.Functions.Worker.Extensions.Binding.svg)](https://badge.fury.io/nu/Axion.Azure.Functions.Worker.Extensions.Binding) of Azure Storage Queues in Azure Functions .NET isolated worker as they appear in WebJob.

### Register binding services
```csharp
services.AddWorkerBinding();
```

### Use IBinder interface via Dependency Injection

```csharp
public class Service(IBinder binder)
{
    public async Task Run()
    {
        var attribute = new QueueAttribute("my-queue")
        {
            Connection = "AzureWebJobsStorage"
        };

        var collector = await binder.BindAsync<IAsyncCollector<string>>(attribute, CancellationToken.None);
        await collector.AddAsync($"Log entry created at: {DateTime.Now}");
    }
}
```
### Use ITypeBinder interface via Dependency Injection

#### Decorate a POCO with a binding attribute
```csharp
[Queue("my-queue")]
class Poco
{
    public string Content { get; set; }
}
```

#### Bind to IAsyncCollector<T>
```csharp
public class Service(ITypeBinder binder)
{
    public async Task Run()
    {
        var collector = await BindAsync<IAsyncCollector<Poco>>();

        await collector.AddAsync(new Poco { Content = $"Log entry created at: {DateTime.Now}" });
    }
}
```

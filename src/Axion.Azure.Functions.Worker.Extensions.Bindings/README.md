## Axion.Azure.Functions.Worker.Extensions.OutputBindings

Support for output bindings and IBinder interface in Azure Functions .NET isolated worker as they appear in WebJobs.

### Register binding services
```csharp
services.AddWorkerOutputBinding();
```

### Use IBinder interface via Dependency Injection

```csharp
public class Service(IBinder binder)
{
    public async Task Run()
    {
        var attribute = new BlobAttribute("samples-workitems/sample.txt", FileAccess.Write)
        {
            Connection = "AzureWebJobsStorage"
        };
        using var writer = await binder.BindAsync<TextWriter>(attribute, CancellationToken.None);
        await writer.WriteLineAsync($"Log entry created at: {DateTime.Now}");
    }
}
```
### Use ITypeBinder interface via Dependency Injection

#### Decorate a POCO with a binding attribute
```csharp
[Blob("samples-workitems/sample.txt", FileAccess.Write)]
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
### Bindings Implementations
#### [Axion.Azure.Functions.Worker.Extensions.Binding.Azure.Storage.Blobs](https://github.com/maksionkin/Axion.Extensions/tree/main/src/Axion.Azure.Functions.Worker.Extensions.Binding.Azure.Storage.Blobs) [![NuGet version](https://badge.fury.io/nu/Axion.Azure.Functions.Worker.Extensions.Binding.Azure.Storage.Blobs.svg)](https://badge.fury.io/nu/Axion.Azure.Functions.Worker.Extensions.Binding.Azure.Storage.Blobs) 
#### [Axion.Azure.Functions.Worker.Extensions.Binding.Azure.Storage.Queues](https://github.com/maksionkin/Axion.Extensions/tree/main/src/Axion.Azure.Functions.Worker.Extensions.Binding.Azure.Storage.Queues) [![NuGet version](https://badge.fury.io/nu/Axion.Azure.Functions.Worker.Extensions.Binding.Azure.Storage.Queues.svg)](https://badge.fury.io/nu/Axion.Azure.Functions.Worker.Extensions.Binding.Azure.Storage.Queues) 

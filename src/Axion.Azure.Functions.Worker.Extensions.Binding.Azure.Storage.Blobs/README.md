## Axion.Azure.Functions.Worker.Extensions.Binding.Azure.Storage.Blobs

Support for [bindings](./src/Axion.Azure.Functions.Worker.Extensions.Binding) [![NuGet version](https://badge.fury.io/nu/Axion.Azure.Functions.Worker.Extensions.Binding.svg)](https://badge.fury.io/nu/Axion.Azure.Functions.Worker.Extensions.Binding) of Azure Storage Blobs in Azure Functions .NET isolated worker as they appear in WebJob.

### Register binding services
```csharp
services.AddWorkerBinding();
```

### Using IBinder interface via Dependency Injection

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

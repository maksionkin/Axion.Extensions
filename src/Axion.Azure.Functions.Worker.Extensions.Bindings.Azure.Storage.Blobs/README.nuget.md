## Axion.Azure.Functions.Worker.Extensions.OutputBindings

Support for output bindings and IBinder interface in Azure Functions .NET isolated worker as they appear in WebJobs.

### Register binding services
```csharp
services.AddWorkerOutputBinding();
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

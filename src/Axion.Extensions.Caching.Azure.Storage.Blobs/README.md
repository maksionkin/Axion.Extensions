# Axion.Extensions.Caching.Azure.Storage.Blobs

[Distributed cache](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache) implementation using Azure Storage Blobs.

Read more [Distributed caching in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed).

# Axion.Extensions.Caching.Azure.Storage.Blobs via NuGet 

[![NuGet version](https://badge.fury.io/nu/Axion.Extensions.Caching.Azure.Storage.Blobs.svg)](https://badge.fury.io/nu/Axion.Extensions.Caching.Azure.Storage.Blobs) 

## Usage Example
### [BlobServiceClient](https://learn.microsoft.com/en-us/dotnet/api/azure.storage.blobs.blobserviceclient) Registration

Register BlobServiceClient ex. [Dependency injection with the Azure SDK for .NET](https://learn.microsoft.com/en-us/dotnet/azure/sdk/dependency-injection)

```csharp
services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(new Uri("< BLOB STORAGE URL>"));

    clientBuilder.UseCredential(new DefaultAzureCredential());
});
```

### Register AzureBlobCache
```csharp
services.AddAzureBlobCache();
```

### Use [IDistributedCache](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache) or [IBufferDistributedCache](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.ibufferdistributedcache) 
```csharp
public class HelloWorldController(IDistributedCache cache) : Controller
{}
```
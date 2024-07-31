## Axion.Extensions.DependencyInjection

Helpers for [Dependency Inection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection).

### Using any delegate to create a service instance
There are cases when one DI service is dependent on some other so it's iniitialization requires getting each service from a container. 

```csharp
services.AddTransient<Service1>();

services.AddTransient<Service2>();

services.AddTransient<Service3>(provider => new DependentService(provider.GetRequiredService<Service1>(), provider.GetService<Service2>()));
```

Last line can be simplified with this nuget as:
```csharp
services.AddTransient<Service3>((Service1 s1, Service2? s2) => new DependentService(s1, s2));
```


[nullable](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references) notation is fully supported.

### Executing a delegate in a service provider context
Instead of typing:
```csharp

var s1 = provider.GetRequiredService<Service1>();
var s2 = provider.GetService<Service2>();
var s3 = provider.GetRequiredService<Service3>();

var a = DoSoething(s1, s2, s3);
```

with the helper can e rewritted as: 
```csharp
var a = provider.Execute((Service1 s1, Service2? s2, Service3 s3) => DoSoething(s1, s2, s3));
```

# Axion.Extensions.DependencyInjection via NuGet 

[![NuGet version](https://badge.fury.io/nu/Axion.Extensions.DependencyInjection.svg)](https://badge.fury.io/nu/Axion.Extensions.DependencyInjection) 

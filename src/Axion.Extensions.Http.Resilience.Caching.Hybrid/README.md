# Axion.Extensions.Http.Resilience.Caching.Hybrid

Caching Resilience mechanism for [HttpClient](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) built on the [Polly](https://github.com/App-vNext/Polly) Caching [Strategy](https://www.pollydocs.org/strategies/index) that utilizes [HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid).

# Axion.Extensions.Http.Resilience.Caching.Hybrid via NuGet 

[![NuGet version](https://badge.fury.io/nu/Axion.Extensions.Http.Resilience.Caching.Hybrid.svg)](https://badge.fury.io/nu/Axion.Extensions.Http.Resilience.Caching.Hybrid) 

## Usage
### Dependency Injection
```csharp
services.AddHybrdCache().AddHttpResponseMessageSerializer();

services.AddHttpClient("name")
    .AddResilienceHandler(
    "name",
    (pipeline, handlerContext) =>
        pipeline.AddCaching(new HttpCachingStrategyOptions()
        {
            HybridCache = handlerContext.ServiceProvider.GetRequiredService<HybridCache>(),
        });
```
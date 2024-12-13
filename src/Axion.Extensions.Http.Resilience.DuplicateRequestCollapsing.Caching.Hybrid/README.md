# Axion.Extensions.Http.Resilience.DuplicateRequestCollapsing.Caching.Hybrid

Collapse concurrent duplicate requests to a single execution Resilience mechanism for [HttpClient](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) built on the [Polly](https://github.com/App-vNext/Polly) Caching [Strategy](https://www.pollydocs.org/strategies/index) that utilizes [HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid).

# Axion.Extensions.Http.Resilience.DuplicateRequestCollapsing.Caching.Hybrid via NuGet 

[![NuGet version](https://badge.fury.io/nu/Axion.Extensions.Http.Resilience.DuplicateRequestCollapsing.Caching.Hybrid.svg)](https://badge.fury.io/nu/Axion.Extensions.Http.Resilience.DuplicateRequestCollapsing.Caching.Hybrid) 

## Usage
### Dependency Injection
```csharp
services.AddHybrdCache().AddHttpResponseMessageSerializer();

services.AddHttpClient("name")
    .AddResilienceHandler(
    "name",
    (pipeline, handlerContext) =>
        pipeline.AddDuplicateRequestCollapsing(new HttpDuplicateRequestCollapsingStrategyOptions()
        {
            HybridCache = handlerContext.ServiceProvider.GetRequiredService<HybridCache>(),
            LockHandleProvider = <CREATE_A_LOCK>,
        });
```
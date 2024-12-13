# Axion.Extensions.Polly.DuplicateRequestCollapsing.Caching.Hybrid

Implementation of [Polly](https://github.com/App-vNext/Polly) collapse concurrent duplicate requests to a single execution [Strategy](https://www.pollydocs.org/strategies/index) that utilizes [HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid).

A result is provided from cache where available.

# Axion.Extensions.Polly.DuplicateRequestCollapsing.Caching.Hybrid via NuGet 

[![NuGet version](https://badge.fury.io/nu/Axion.Extensions.Polly.DuplicateRequestCollapsing.Caching.Hybrid.svg)](https://badge.fury.io/nu/Axion.Extensions.Polly.DuplicateRequestCollapsing.Caching.Hybrid) 

## Usage
### Dependency Injection
```csharp
services.AddHybrdCache();

services.AddResiliencePipeline<string, TYPE>(
    "test-pipeline",
    (pipeline, add) =>
    pipeline.AddDuplicateRequestCollapsing(
        new()
        {
            LockHandleProvider = <CREATE_A_LOCK>;_
            HybridCache = add.ServiceProvider.GetRequiredService<HybridCache>(),
        }));
```
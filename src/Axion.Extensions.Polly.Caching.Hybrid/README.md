# Axion.Extensions.Polly.Caching.Hybrid

Implementation of [Polly](https://github.com/App-vNext/Polly) Caching [Strategy](https://www.pollydocs.org/strategies/index) that utilizes [HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid).
A result is provided from cache where available.

# Axion.Extensions.Polly.Caching.Hybrid via NuGet 

[![NuGet version](https://badge.fury.io/nu/Axion.Extensions.Polly.Caching.Hybrid.svg)](https://badge.fury.io/nu/Axion.Extensions.Polly.Caching.Hybrid) 

## Usage
### Dependency Injection
```csharp
services.AddHybrdCache();

services.AddResiliencePipeline<string, TYPE>(
    "test-pipeline",
    (pipeline, add) =>
    pipeline.AddCaching(
        new()
        {
            HybridCache = add.ServiceProvider.GetRequiredService<HybridCache>(),
        }));
```

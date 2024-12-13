# Axion.Extensions.Polly.Caching.Hybrid

Implementation of [Polly](https://github.com/App-vNext/Polly) Caching [Strategy](https://www.pollydocs.org/strategies/index) that utilizes [HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid).

A result is provided from cache where available.

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

# Axion.Extensions.Caching.Hybrid.Serialization.Http

A plugin for [HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid) to serialize/deserialize [System.Net.Http.HttpResponseMessage](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpresponsemessage).

Contains an implementaton of [IHybridCacheSerializer](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.hybrid.ihybridcacheserializer-1)<[HttpResponseMessage](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpresponsemessage)>.

# Axion.Extensions.Caching.Transformed via NuGet 

[![NuGet version](https://badge.fury.io/nu/Axion.Extensions.Caching.Hybrid.Serialization.Http.svg)](https://badge.fury.io/nu/Axion.Extensions.Caching.Hybrid.Serialization.Http) 

## Usage

```csharp
services.AddHybrdCache().AddHttpResponseMessageSerializer();
```

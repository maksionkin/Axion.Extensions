# Axion.Extensions.Polly.Caching.Serialization.Http

A plugin for the [Polly](https://github.com/App-vNext/Polly) [Cache policy](https://github.com/App-vNext/Polly/wiki/Cache) to serialize/deserialize [System.Net.Http.HttpResponseMessage](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpresponsemessage)
Can be used with [Microsoft.Extensions.Http.PolicyHttpMessageHandler](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.http.policyhttpmessagehandler)

# Installing Axion.Extensions.Polly.Caching.Serialization.Http via NuGet [![NuGet version](https://badge.fury.io/nu/Axion.Extensions.Polly.Caching.Serialization.Http.svg)](https://badge.fury.io/nu/Axion.Extensions.Polly.Caching.Serialization.Http) 

    Install-Package Axion.Extensions.Polly.Caching.Serialization.Http

# Serialization Format

The [HttpResponseMessageSerializer](./HttpResponseMessageSerializer.cs) uses a format similar to [HTTP 1.1](https://www.rfc-editor.org/rfc/rfc9112.html)
Trailing headers go after the body.
When trailing headers are being deserialized on a platform withot trailing headers support they are added to the response or content headers.

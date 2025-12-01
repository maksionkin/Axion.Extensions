// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using static Axion.Extensions.FileProviders.GitSmartHttpFileProvider;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Net.Http;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for associating and retrieving request-specific information with an <see
/// cref="HttpRequestMessage"/> instance.
/// </summary>
/// <remarks>These extension methods enable attaching custom metadata to HTTP requests, which can be useful for
/// tracking request state or passing additional context through the request pipeline. The storage mechanism used
/// depends on the target .NET version, but the API remains consistent for consumers.</remarks>
public static class HttpRequestMessageExtensions
{
    extension(HttpRequestMessage request)
    {
        internal void SetRequestInfo(RequestInfo requestInfo) =>
#if NET5_0_OR_GREATER
            request.Options.Set(new HttpRequestOptionsKey<RequestInfo>(HttpRequestOptionsKey), requestInfo);
#else
            request.Properties[HttpRequestOptionsKey] = requestInfo;
#endif

        /// <summary>
        /// Gets the request-specific information associated with the current Git HTTP Smart request, if available.
        /// </summary>
        /// <returns>A <see cref="RequestInfo"/> object containing details about the request if present; otherwise, <see
        /// langword="null"/>.</returns>
        public RequestInfo? GetRequestInfo() =>
#if NET5_0_OR_GREATER
            request.Options.TryGetValue(new HttpRequestOptionsKey<RequestInfo>(HttpRequestOptionsKey), out var o)
                ? o
                : null;
#else
            request.Properties.TryGetValue(HttpRequestOptionsKey, out var o)
                ? o as RequestInfo
                : null;
#endif        

    }
}

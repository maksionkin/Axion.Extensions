// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Net.Http;
#pragma warning restore IDE0130 // Namespace does not match folder structure

static class HttpContentExtensions
{
    extension(HttpContent content)
    {
#if !NET5_0_OR_GREATER
        public Task<Stream> ReadAsStreamAsync(CancellationToken cancellationToken) =>
            content.ReadAsStreamAsync();
#endif
    }
}

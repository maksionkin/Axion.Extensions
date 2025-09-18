// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// An implementation of <see cref="INameResolver"/> that resolves tokens by looking into <see cref="IConfiguration"/>.
    /// </summary>
    public class DefaultNameResolver(IConfiguration configuration) : INameResolver
    {

        /// <summary>
        /// Resolves tokens by looking first in App Settings and then in environment variables.
        /// </summary>
        /// <param name="name">The token to resolve.</param>
        /// <returns>The token value from configuration. If the token is not found, null is returned.</returns>
        public virtual string? Resolve(string? name) =>
            string.IsNullOrEmpty(name)
                ? null
                : configuration?[name!];
    }
}

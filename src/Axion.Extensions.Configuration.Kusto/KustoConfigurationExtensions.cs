// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using Axion.Extensions.Configuration;
using Kusto.Data.Common;

namespace Microsoft.Extensions.Configuration;

/// <summary>
/// Extension methods for adding <see cref="KustoConfigurationProvider"/>.
/// </summary>
public static class JsonConfigurationExtensions
{
    /// <summary>
    /// Adds a Kusto configuration source to <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="connectionString">A Kusto cluster connection string.</param>
    /// <param name="query">A Kuso query.</param>
    /// <param name="configureSource">Configures the source.</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddKusto(this IConfigurationBuilder builder, string connectionString, string query, Action<KustoConfigurationSource>? configureSource = null)
    {
        var kustoConfigurationSource = new KustoConfigurationSource(connectionString, query);

        configureSource?.Invoke(kustoConfigurationSource);

        return builder.Add(kustoConfigurationSource);
    }
    /// <summary>
    /// Adds a Kusto configuration source to <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="getCslQueryProvider">A cretor of <see cref="ICslQueryProvider"/> instance.</param>
    /// <param name="query">A Kuso query.</param>
    /// <param name="configureSource">Configures the source.</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddKusto(this IConfigurationBuilder builder, Func<ICslQueryProvider> getCslQueryProvider, string query, Action<KustoConfigurationSource>? configureSource = null)
    {
        var kustoConfigurationSource = new KustoConfigurationSource(getCslQueryProvider, query);

        configureSource?.Invoke(kustoConfigurationSource);

        return builder.Add(kustoConfigurationSource);
    }
}

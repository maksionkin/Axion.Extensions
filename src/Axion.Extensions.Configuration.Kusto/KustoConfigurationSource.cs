// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using CommunityToolkit.Diagnostics;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Microsoft.Extensions.Configuration;

namespace Axion.Extensions.Configuration;

/// <summary>
/// <see cref="IConfigurationSource"/> implementation based on result of Kusto query.
/// </summary>
public class KustoConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Initializes a new instance of <see cref="KustoConfigurationSource"/>.
    /// </summary>
    /// <param name="getCslQueryProvider">A factory to get an <see cref="ICslQueryProvider"/>.</param>
    /// <param name="query">A Kusto query to execute to get configuration values.</param>
    public KustoConfigurationSource(Func<ICslQueryProvider> getCslQueryProvider, string query)
    {
        Guard.IsNotNull(getCslQueryProvider);
        Guard.IsNotNull(query);

        Query = query;
        GetCslQueryProvider = getCslQueryProvider;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="KustoConfigurationSource"/>.
    /// </summary>
    /// <param name="connectionString">A connection string to a Kusto cluster.</param>
    /// <param name="query">A Kusto query to execute to get configuration values.</param>
    public KustoConfigurationSource(string connectionString, string query)
     : this(CreateGetCslQueryProvider(connectionString), query)
    {
    }

    static Func<ICslQueryProvider> CreateGetCslQueryProvider(string connectionString)
    {
        Guard.IsNotNull(connectionString);

        return () => KustoClientFactory.CreateCslQueryProvider(connectionString);
    }


    /// <summary>
    /// A prefix for configuration keys. 
    /// </summary>
    public string KeyPrefix { get; set; } = "Value";


    /// <summary>
    /// The periodic interval to refresh Kusto configuration. Default is 30 minutes.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// <see langword="true"> if periodic refresh is disabled.</see>
    /// </summary>
    public bool DisablePeriodicRefresh { get; set; }

    /// <summary>
    /// The query to the Kusto cluster.
    /// </summary>
    public string Query { get; }


    /// <summary>
    /// A <see cref="ICslQueryProvider"/> factory method.
    /// </summary>
    public Func<ICslQueryProvider> GetCslQueryProvider { get; }


    /// <inheritdoc/>
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
       new KustoConfigurationProvider(this);
}

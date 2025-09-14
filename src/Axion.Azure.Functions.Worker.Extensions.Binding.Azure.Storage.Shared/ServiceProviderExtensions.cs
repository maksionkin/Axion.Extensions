// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using Azure.Identity;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace System;

static partial class ServiceProviderExtensions
{
    static readonly Lazy<AzureComponentFactory> AzureComponentFactory = new(() =>
    {
        var services = new ServiceCollection();
        services.AddAzureClients(builder => builder.UseCredential(new DefaultAzureCredential()));

        return services.BuildServiceProvider().GetRequiredService<AzureComponentFactory>();
    });

    public static AzureComponentFactory GetAzureComponentFactory(this IServiceProvider serviceProvider) =>
        serviceProvider.GetService<AzureComponentFactory>() ?? AzureComponentFactory.Value;

    public static IConfigurationSection GetConfigurationSection(this IServiceProvider serviceProvider, string? connection) =>
        serviceProvider.GetRequiredService<IConfiguration>().GetSection(connection ?? "AzureWebJobsStorage");
}

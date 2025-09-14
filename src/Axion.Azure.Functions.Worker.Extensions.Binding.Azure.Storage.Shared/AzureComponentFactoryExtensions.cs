// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using Azure.Core;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Azure;

static partial class AzureComponentFactoryExtensions
{
    public static TOptions CreateClientOptions<TOptions>(this AzureComponentFactory azureComponentFactory, IConfiguration configuration)
        where TOptions : ClientOptions =>
        (TOptions)azureComponentFactory.CreateClientOptions(typeof(TOptions), null, configuration);

}

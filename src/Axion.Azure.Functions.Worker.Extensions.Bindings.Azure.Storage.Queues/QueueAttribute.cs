// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Axion.Azure.Functions.Worker;
using Azure.Storage.Queues;
using CommunityToolkit.Diagnostics;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Azure.WebJobs;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// An attribute used to bind a parameter to an Azure Storage Queue, enabling interaction with the queue as an output
/// binding in Azure Functions.
/// </summary>
/// <remarks>This attribute can be applied to parameters of type <see cref="QueueClient"/> or <see
/// cref="IAsyncCollector{T}"/> where T is <see cref="string"/>. It supports binding to a specific Azure Storage Queue
/// and provides options for configuring the connection and queue creation behavior.</remarks>
/// <param name="queueName">The name of the Azure Storage Queue to bind to. This value must not be null or empty.</param>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
[DebuggerDisplay("{" + nameof(QueueName) + ",nq}")]
public sealed class QueueAttribute(string queueName)
    : BindingAttribute([typeof(QueueClient), typeof(IAsyncCollector<string>)])
{
    const string FunctionsApplicationDirectoryKey = "FUNCTIONS_APPLICATION_DIRECTORY";
    const string HostJsonFileName = "host.json";
    const string MessageEncodingNone = "none";

    static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Gets the name of the Azure Storage Queue to bind to.
    /// </summary>
    public string QueueName { get; } = queueName;

    /// <summary>
    /// Gets or sets the connection string or connection name to the Azure Storage Queue.
    /// </summary>
    public string? Connection { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue should be created if it does not exist.
    /// </summary>
    public bool CreateIfNotExists { get; set; } = true;

    /// <inheritdoc />
    protected override async ValueTask<object> BindAsync(IServiceProvider serviceProvider, Type type, CancellationToken cancellationToken)
    {
        Guard.IsNotNull(serviceProvider);
        Guard.IsNotNull(type);

        var hostFolder = serviceProvider.GetRequiredService<IConfiguration>().GetSection(FunctionsApplicationDirectoryKey)?.Value
            ?? Environment.GetEnvironmentVariable(FunctionsApplicationDirectoryKey);

        var messageEncoding = QueueMessageEncoding.Base64;
        if (hostFolder != null)
        {
            var hostFile = Path.Combine(hostFolder, HostJsonFileName);

            if (File.Exists(hostFile)
                && JsonSerializer.Deserialize<HostJson>(File.ReadAllText(hostFile), JsonSerializerOptions)
                    ?.Extensions
                    ?.Queues
                    ?.MessageEncoding == MessageEncodingNone)
            {
                messageEncoding = QueueMessageEncoding.None;
            }
        }

        var nameResolver = serviceProvider.GetService<INameResolver>();
        var (queueName, connection) = nameResolver == null
            ? (QueueName, Connection)
            : (nameResolver.ResolveWholeString(QueueName), nameResolver.ResolveWholeString(Connection));


        var azureComponentFactory = serviceProvider.GetAzureComponentFactory();

        var configuration = serviceProvider.GetConfigurationSection(connection);
        var clientOptions = azureComponentFactory.CreateClientOptions<QueueClientOptions>(configuration);

        clientOptions.MessageEncoding = messageEncoding;

        QueueServiceClient queueServiceClient;

        if (connection?.Contains('=') == true)
        {
            queueServiceClient = new QueueServiceClient(connection, clientOptions);
        }
        else
        {
            if (configuration is IConfigurationSection section && section.Value is string cs)
            {
                queueServiceClient = new QueueServiceClient(cs, clientOptions);
            }
            else
            {
                var queueConfiguration = new QueueConfiguration();

                configuration.Bind(queueConfiguration);

                if (queueConfiguration.QueueServiceUri is null)
                {
                    queueServiceClient = (QueueServiceClient)azureComponentFactory.CreateClient(typeof(QueueServiceClient), configuration, null, clientOptions);
                }
                else
                {
                    queueServiceClient = new QueueServiceClient(queueConfiguration.QueueServiceUri, clientOptions);
                }
            }
        }

        var queueClient = queueServiceClient.GetQueueClient(queueName);
        if (type == typeof(QueueClient))
        {
            if (CreateIfNotExists)
            {
                await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }

            return queueClient;
        }
        else if (type == typeof(IAsyncCollector<string>))
        {
            return new StringAsyncCollector(queueClient, CreateIfNotExists);
        }
        else
        {
            throw new InvalidOperationException($"Cannot bind to {type.FullName} using {GetType().FullName}.");
        }
    }

    record HostJson(HostJsonExtensions? Extensions);

    record HostJsonExtensions(HostJsonExtensionsQueues Queues);

    record HostJsonExtensionsQueues(string? MessageEncoding);
    class QueueConfiguration
    {
        public string? AccountName { get; set; }

        public Uri? QueueServiceUri
        {
            get
            {

                if (field != null)
                {
                    return field;
                }

                if (!string.IsNullOrEmpty(AccountName))
                {
                    return new Uri($"https://{AccountName}.queue.core.windows.net/");
                }

                return null;
            }
            set
            {
                field = value;
            }
        }
    }
}

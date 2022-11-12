// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kusto.Cloud.Platform.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Axion.Extensions.Configuration;

/// <summary>
/// <see cref="ConfigurationProvider"/> implementation based on result of Kusto query.
/// </summary>
public class KustoConfigurationProvider : ConfigurationProvider, IDisposable
{
    readonly KustoConfigurationSource kustoConfigurationSource;
    readonly CancellationTokenSource? source;

    bool disposedValue;

    /// <summary>
    /// Initializes a new instance of <see cref="KustoConfigurationProvider"/>.
    /// </summary>
    /// <param name="kustoConfigurationSource">An instance of <see cref="KustoConfigurationSource"/>.</param>
    public KustoConfigurationProvider(KustoConfigurationSource kustoConfigurationSource)
    {
        this.kustoConfigurationSource = kustoConfigurationSource;
        if (!kustoConfigurationSource.DisablePeriodicRefresh)
        {
            source = new();
            RefreshAsync(source.Token);
        }
    }

    async void RefreshAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var wait = kustoConfigurationSource.RefreshInterval;
            if (wait <= TimeSpan.Zero)
            {
                wait = TimeSpan.FromMinutes(30);
            }

            try
            {
                await Task.Delay(wait, cancellationToken);

                Load();
            }
            catch { }
        }
    }

    /// <inheritdoc/>
    public override void Load()
    {
        using var memoryStream = new MemoryStream();
        {
            using var textWriter = new StreamWriter(memoryStream, Encoding.UTF8, 8192, true);
            using var jsonWriter = new JsonTextWriter(textWriter) { AutoCompleteOnClose = true, CloseOutput = false, Formatting = Formatting.None};

            foreach (var part in kustoConfigurationSource.KeyPrefix.Split(':'))
            {
                jsonWriter.WriteStartObject();
                jsonWriter.WritePropertyName(part);
            }

            jsonWriter.WriteStartArray();

            using var provider = kustoConfigurationSource.GetCslQueryProvider();
            using var dataReader = provider.ExecuteQuery(kustoConfigurationSource.Query);

            foreach (var jobject in dataReader.ToJObjects())
            {
                using var reader = jobject.CreateReader();
                jsonWriter.WriteToken(reader, true);
            }
        }

        memoryStream.Position = 0;

        var jsonConfigurationProvider = new JsonStreamConfigurationProviderEx(memoryStream);
        jsonConfigurationProvider.Load(memoryStream);

        Data = jsonConfigurationProvider.GetData();

        OnReload();
    }

    /// <summary>
    /// Dispose the instance.
    /// </summary>
    /// <param name="disposing">true if the motod is called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                source?.Cancel(false);

                source?.Dispose();
            }

            disposedValue = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    class JsonStreamConfigurationProviderEx : JsonStreamConfigurationProvider
    {
        public JsonStreamConfigurationProviderEx(Stream stream)
            : base(new JsonStreamConfigurationSource { Stream = stream })
        { }

        public IDictionary<string, string?> GetData() =>
            Data;
    }
}

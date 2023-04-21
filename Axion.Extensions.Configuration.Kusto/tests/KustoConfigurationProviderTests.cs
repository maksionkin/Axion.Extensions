using System.Data;
using Kusto.Cloud.Platform.Utils;
using Kusto.Data.Common;
using Kusto.Data.Results;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace Axion.Extensions.Configuration.Kusto.Tests
{
    [TestClass]
    public class KustoConfigurationProviderTests
    {
        [TestMethod]
        public void TestKustoConfigurationProvider()
        {
            using var dataTable = new DataTable();
            dataTable.Columns.Add("string", typeof(string));
            dataTable.Columns.Add("int", typeof(int));
            dataTable.Columns.Add("bool", typeof(bool));
            dataTable.Columns.Add("JObject", typeof(JObject));

            dataTable.Rows.Add("full", 6, true, JObject.Parse("{'a':2}"));
            dataTable.Rows.Add("nulls", null, null, null);


            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddKusto(() => new KustoDataTableProvider(dataTable), "project", source => source.DisablePeriodicRefresh = true);

            var configuration = configurationBuilder.Build();
            var configurationValue = configuration.Get<ConfigurationValue>();
            
            Assert.AreEqual(configurationValue.Value?.Count, 2);
            Assert.AreEqual(configurationValue.Value?[0].JObject?.A, 2);
        }

        class ConfigurationValue
        {
            public IList<ConfigurationRecord>? Value { get; set; }
            public class ConfigurationRecord
            {
                public string? String { get; set; }
                public int? Int { get; set; }
                public bool? Bool { get; set; }
                public ConfigurationA? JObject { get; set; }
            }

            public class ConfigurationA
            {
                public int A { get; set; }
            }
        }

        class KustoDataTableProvider : ICslQueryProvider, IDisposable
        {
            readonly DataTable dataTable;

            bool disposedValue;

            public KustoDataTableProvider(DataTable dataTable) =>
                this.dataTable = dataTable;
            public string DefaultDatabaseName 
            { 
                get => dataTable.TableName; 
                set => dataTable.TableName = value; 
            }

            public IDataReader ExecuteQuery(string databaseName, string query, ClientRequestProperties properties) =>
                ExecuteQuery();
            public IDataReader ExecuteQuery(string query, ClientRequestProperties properties) =>
                ExecuteQuery();

            public IDataReader ExecuteQuery(string query) =>
                ExecuteQuery();

            IDataReader ExecuteQuery() =>
                dataTable.CreateDataReader();

            public Task<IDataReader> ExecuteQueryAsync(string databaseName, string query, ClientRequestProperties properties) =>
                Task.FromResult(ExecuteQuery());

            public Task<ProgressiveDataSet> ExecuteQueryV2Async(string databaseName, string query, ClientRequestProperties properties)=>
                throw new NotImplementedException();

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                    }

                    disposedValue = true;
                }
            }

             public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }
    }
}

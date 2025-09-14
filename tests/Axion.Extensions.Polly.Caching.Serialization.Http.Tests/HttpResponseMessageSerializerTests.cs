// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace Axion.Extensions.Polly.Caching.Serialization.Http.Tests;

[TestClass]
public class HttpResponseMessageSerializerTests
{
    static readonly Encoding Utf8 = new UTF8Encoding(false);

    [DataTestMethod]
    [DataRow(null, null, null)]
    [DataRow(null, "Head", null)]
    [DataRow(null, "Options", null)]
    [DataRow("1.0", null, null)]
    [DataRow("1.0", "Head", null)]
    [DataRow("1.0", "Options", null)]
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
    [DataRow("2.0", null, null)]
    [DataRow("2.0", "Head", null)]
    [DataRow("2.0", "Options", null)]
#endif
    [DataRow(null, "Trace", new byte[] { 1, 2, 3, 4, 5, 6 })]
    public void Run(string? version = null, string? method = null, byte[]? payload = null)
    {
        const string url = "https://example.com/";

        using var httpClient = new HttpClient();

        using var request = new HttpRequestMessage(new(method ?? "get"), url) { Version = new(version ?? "1.1") };
        if (payload != null)
        {
            request.Content = new ByteArrayContent(payload);
        }

        using var response = httpClient.SendAsync(request).Result;

        var bytes = HttpResponseMessageSerializer.Instance.Serialize(response);

        using var deserialized = HttpResponseMessageSerializer.Instance.Deserialize(bytes);

        Assert.AreEqual(response.Version, deserialized.Version);
        Assert.AreEqual(response.StatusCode, deserialized.StatusCode);
        Assert.AreEqual(response.ReasonPhrase, deserialized.ReasonPhrase);

        Assert.AreEqual(response.Headers.ToString(), deserialized.Headers.ToString());

        Assert.AreEqual(response.Content.Headers.ToString(), deserialized.Content.Headers.ToString());
        Assert.AreEqual(response.Content.ReadAsStringAsync().Result, deserialized.Content.ReadAsStringAsync().Result);
    }

    [DataTestMethod]
    [DataRow("Chuncked.txt", true, false)]
    [DataRow("ChunckedBad.txt", false, false)]
    [DataRow("WithLength.txt", true, false)]
    [DataRow("WithoutLength.txt", false, false)]
    [DataRow("Chuncked.txt", true, true)]
    [DataRow("ChunckedBad.txt", false, true)]
    [DataRow("WithLength.txt", true, true)]
    [DataRow("WithoutLength.txt", false, true)]
    public void TestComplexResponse(string file, bool hasTrailing, bool roundTrip)
    {
        var bytes = Utf8.GetBytes(string.Join("\r\n", File.ReadAllLines(file)));

        var response = HttpResponseMessageSerializer.Instance.Deserialize(bytes);
        if (roundTrip)
        {
            response = HttpResponseMessageSerializer.Instance.Deserialize(HttpResponseMessageSerializer.Instance.Serialize(response));
        }

        Assert.AreEqual((HttpStatusCode)234, response.StatusCode);
        Assert.AreEqual("Some status", response.ReasonPhrase);

        Assert.IsTrue(response.Headers.TryGetValues("X-MultiLine", out var values));
        Assert.AreEqual(2, values.Count());

        Assert.AreEqual("line0", values.ElementAt(0));
        Assert.AreEqual("line1", values.ElementAt(1));

        var content = response.Content.ReadAsStringAsync().Result;

        Assert.AreEqual("12hgefghdsghjvhs+dvjfffffffffffffffa123", content);

        if (hasTrailing)
        {
            var trailingHeaders = response.Headers;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            trailingHeaders = response.TrailingHeaders;
#endif


            var frameworkName = new FrameworkName(typeof(HttpResponseMessageSerializer).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
            if (frameworkName.Identifier.Equals(".NETStandard", StringComparison.OrdinalIgnoreCase) && frameworkName.Version < new Version(2, 1)
                || frameworkName.Identifier.Equals(".NETCoreApp", StringComparison.OrdinalIgnoreCase) && frameworkName.Version < new Version(3, 0))
            {
                trailingHeaders = response.Headers;
            }

            Assert.IsTrue(trailingHeaders.TryGetValues("x-trailing", out values));

            Assert.AreEqual(values.Count(), 1);
            Assert.AreEqual(values.ElementAt(0), "trailing");
        }
    }

    [TestMethod]
    public async Task Deserialize_Response_Supports_Pipeline()
    {
        var serializer = HttpResponseMessageSerializer.Instance;

        var originalContent = new DataRecord("Test content for pipeline");

        using var response = new HttpResponseMessage(HttpStatusCode.OK);

        response.Content = new StringContent(JsonSerializer.Serialize(originalContent), Encoding.UTF8, MediaTypeNames.Text.Plain);

        // Simulate pipeline: first handler reads the content.
        var bytes = serializer.Serialize(response);
        Assert.IsTrue(bytes.Length > 0);

        // Simulate next handler in pipeline: read content again.
        using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var streamReader = new StreamReader(responseStream);
        var typedBody = JsonSerializer.Deserialize<DataRecord>(streamReader.ReadToEnd());

        Assert.IsNotNull(typedBody, "Message could not be read a second time in a simulated pipeline.");
        Assert.AreEqual(originalContent, typedBody!);
    }

    record DataRecord(string Value);
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

namespace Axion.Extensions.Polly.Caching.Serialization.Http.Tests;

[TestClass]
public class HttpResponseMessageSerializerTests
{
    static void Run(string url, Version? version = null, HttpMethod? method = null, byte[]? payload = null)
    {
        using var httpClient = new HttpClient();

        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, url) { Version = version ?? new Version(1, 1) };
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

    [TestMethod]
    public void TestWikipedia()
    {
        Run("https://www.wikipedia.org/");
        Run("https://www.wikipedia.org/", method: HttpMethod.Head);
        Run("https://www.wikipedia.org/", method: HttpMethod.Options);
        Run("https://www.wikipedia.org/", new(1, 0));
        Run("https://www.wikipedia.org/", new(2, 0));

        Run("https://www.wikipedia.org/", new(2, 0), HttpMethod.Options);

        Run("https://www.wikipedia.org/", new(2, 0), HttpMethod.Trace, Encoding.UTF8.GetBytes("TEST"));
    }
    [TestMethod]
    public void TestComplexResponse()
    {
        using var ms = new MemoryStream();
        using (var sw = new StreamWriter(ms) { NewLine = "\r\n" })
        {
            foreach (var line in File.ReadAllLines("ComplexRespose.txt"))
            {
                sw.WriteLine(line);
            }
        }

        var bytes = ms.ToArray();

        using var response = HttpResponseMessageSerializer.Instance.Deserialize(bytes);

        Assert.IsTrue(response.Headers.TryGetValues("X-MultiLine", out var values));
        Assert.AreEqual(values.Count(), 2);

        Assert.AreEqual(values.ElementAt(0), "line0");
        Assert.AreEqual(values.ElementAt(1), "line1");

        Assert.IsTrue(response.TrailingHeaders.TryGetValues("x-trailing", out values));
        Assert.AreEqual(values.Count(), 1);
        Assert.AreEqual(values.ElementAt(0), "trailing");

        Assert.AreEqual(response.Content.ReadAsStringAsync().Result, "12hgefghdsghjvhs+dvjfffffffffffffffa123");
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

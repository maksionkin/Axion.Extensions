using System.Buffers;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.IO;

namespace Axion.Extensions.Caching.Hybrid.Serialization.Http.Tests;

[TestClass]
public class SerializerTests
{
    static readonly RecyclableMemoryStreamManager RecyclableMemoryStreamManager = new();

    static readonly Encoding Utf8 = new UTF8Encoding(false);

    static readonly int[] Fibonacci = [0, 1, 1, 2, 3, 5, 8, 13, 21];

    static ReadOnlySequence<byte> Load(string file, int seed)
    {
        var bytes = Utf8.GetBytes(string.Join("\r\n", File.ReadAllLines(file)));

        var random = new Random(seed);

        var start = Segment.GetSegment(bytes, 0, random)!;

        ReadOnlySequenceSegment<byte>? end = start;
        while (end.Next != null)
        {
            end = end.Next;
        }


        return new ReadOnlySequence<byte>(start, 0, end, end.Memory.Length);
    }

    [DataTestMethod]
    [DataRow("Chuncked.txt", 28, true, false, 0)]
    [DataRow("ChunckedBad.txt", 22, false, false, 32)]
    [DataRow("WithLength.txt", 13, true, false, 512)]
    [DataRow("WithoutLength.txt", 6, false, false, null)]
    [DataRow("Chuncked.txt", 6, true, true, 0)]
    [DataRow("ChunckedBad.txt", 13, false, true, 32)]
    [DataRow("WithLength.txt", 22, true, true, 512)]
    [DataRow("WithoutLength.txt", 28, false, true, null)]
    public void TestResponseRead(string file, int seed, bool hasTrailing, bool roundTrip, int? stackSize)
    {
        var instance = stackSize == null ? HttpResponseMessageHybridCacheSerializer.Instance : new(new HttpResponseMessageHybridCacheSerializer.Options() { MaxCharsOnStack = stackSize.Value });

        var response = instance.Deserialize(Load(file, seed));

        if (roundTrip)
        {
            var ms = new RandomChunckBufferWriter(new(seed));
            instance.Serialize(response, ms);

            var s = ms.ToString();

            response = instance.Deserialize(ms.GetSequence());
        }

        Assert.AreEqual((HttpStatusCode)234, response.StatusCode);
        Assert.AreEqual("Some status👍", response.ReasonPhrase);

        Assert.IsTrue(response.Headers.TryGetValues("X-a", out var values));
        Assert.IsTrue(response.Headers.TryGetValues("X-b", out var values2));
        Assert.AreSame(values.ElementAt(0), values2.ElementAt(0));

        Assert.IsTrue(response.Headers.TryGetValues("X-MultiLine", out values));
        Assert.AreEqual(2, values.Count());

        Assert.AreEqual("line0", values.ElementAt(0));
        Assert.AreEqual("line1", values.ElementAt(1));

        Assert.IsTrue(response.Headers.TryGetValues("x-987654321098765432109876543210987654321098765432109876543210987654321098765432109876543210", out values));
        Assert.AreEqual(1, values.Count());

        Assert.AreEqual("👍987654321098765432109876543210987654321098765432109876543210987654321098765432109876543210", values.ElementAt(0));

        var content = response.Content.ReadAsStringAsync().Result;

        Assert.AreEqual("12hgefghdsghjvhs+dvjfffffffffffffffa123", content);

        if (hasTrailing)
        {
            var trailingHeaders = response.Headers;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            trailingHeaders = response.TrailingHeaders;
#endif


            var frameworkName = new FrameworkName(typeof(HttpResponseMessageHybridCacheSerializer).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
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


    class Segment : ReadOnlySequenceSegment<byte>
    {
        public static Segment? GetSegment(byte[] bytes, int start, Random random)
        {
            if (start >= bytes.Length)
            {
                return null;
            }

            var cur = random.GetItem(Fibonacci);

            var length = Math.Min(cur, bytes.Length - start);

            var segment = new Segment()
            {
                Memory = new ReadOnlyMemory<byte>(bytes, start, length),
                Next = GetSegment(bytes, start + length, random),
                RunningIndex = start,
            };

            return segment;
        }
    }

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

        httpClient.Timeout = TimeSpan.FromDays(1);

        using var request = new HttpRequestMessage(new(method ?? "get"), url) { Version = new(version ?? "1.1") };
        if (payload != null)
        {
            request.Content = new ByteArrayContent(payload);
        }

        var response = httpClient.SendAsync(request).Result;
        using var ms = RecyclableMemoryStreamManager.GetStream(url);
        HttpResponseMessageHybridCacheSerializer.Instance.Serialize(response, ms);

        using var deserialized = HttpResponseMessageHybridCacheSerializer.Instance.Deserialize(new ReadOnlySequence<byte>(ms.ToArray()));

        Assert.AreEqual(response.Version, deserialized.Version);
        Assert.AreEqual(response.StatusCode, deserialized.StatusCode);
        Assert.AreEqual(response.ReasonPhrase, deserialized.ReasonPhrase);

        Assert.AreEqual(response.Headers.ToString(), deserialized.Headers.ToString());

        Assert.AreEqual(response.Content.Headers.ToString(), deserialized.Content.Headers.ToString());
        Assert.AreEqual(response.Content.ReadAsStringAsync().Result, deserialized.Content.ReadAsStringAsync().Result);
    }

    class RandomChunckBufferWriter(Random random) : IBufferWriter<byte>
    {
        int current;
        readonly List<byte[]> chunks = [[]];

        public void Advance(int count)
        {
            current += count;

            if (chunks.Last().Length < current)
            {
                chunks.Add(new byte[current - chunks.Last().Length]);

                current = 0;
            }
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Get(sizeHint);

            return new(chunks.Last(), current, chunks.Last().Length - current);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            Get(sizeHint);

            return new(chunks.Last(), current, chunks.Last().Length - current);
        }

        void Get(int sizeHint)
        {
            Assert.AreEqual(0, sizeHint);

            if (current >= chunks.Last().Length)
            {
                var r = random.GetItem(Fibonacci);
                var array = new byte[1 + r];

                chunks.Add(array);

                current = 0;
            }
        }

        public ReadOnlySequence<byte> GetSequence()
        {
            var last = chunks.Last();

            Array.Resize(ref last, current);
            chunks[^1] = last;

            return new(chunks.SelectMany(l => l).ToArray());
        }

        public override string ToString() =>
            Utf8.GetString(GetSequence().ToArray());
    }
}

static class RandomExtensions
{
    public static T GetItem<T>(this Random random, T[] values) =>
        values[random.Next(values.Length)];
}



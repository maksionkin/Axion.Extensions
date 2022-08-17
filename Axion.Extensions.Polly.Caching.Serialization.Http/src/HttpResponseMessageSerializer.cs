// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Dawn;
using Polly.Caching;

namespace Axion.Extensions.Polly.Caching.Serialization.Http;

/// <summary>
/// A serializer for serializing items of type <see cref="HttpResponseMessage"/>, for the Polly <see cref="CachePolicy"/>
/// </summary>
public class HttpResponseMessageSerializer : ICacheItemSerializer<HttpResponseMessage, byte[]>
{
    /// <summary>
    /// Gets an instance of <see cref="HttpResponseMessageSerializer"/>
    /// </summary>
    public static readonly HttpResponseMessageSerializer Instance = new();

    const char CR = '\r'; // <US-ASCII CR, carriage return (13)>
    const char LF = '\n'; // <US-ASCII LF, linefeed (10)>
    const char SP = ' '; // <US-ASCII SP, space (32)>
    const char HT = '\t'; // <US-ASCII HT, horizontal-tab(9)>

    const char HeaderSeparator = ':';

    static readonly string CRLF = new(new[] { CR, LF });
    static readonly char[] Spaces = new[] { SP, HT };

    static readonly UTF8Encoding Utf8 = new(false, false);

    const string HttpResponseSignature = "HTTP/";

    /// <inheritdoc/>
    public HttpResponseMessage Deserialize(byte[] objectToDeserialize)
    {
        Guard.Argument(objectToDeserialize)
            .NotNull()
            .MinCount(HttpResponseSignature.Length + 1 + CRLF.Length);

        int GetCount(int start, int? end) =>
            (end == null || end.Value < 0 || end.Value >= objectToDeserialize.Length
                ? objectToDeserialize.Length
                : end.Value) - start;

        string GetString(int start, int? end) =>
            Utf8.GetString(objectToDeserialize, start, GetCount(start, end));

        int? GetNextCRLF(int start)
        {
            while (true)
            {
                if (start >= objectToDeserialize.Length)
                {
                    return null;
                }

                var indexOfCR = Array.IndexOf(objectToDeserialize, (byte)CR, start);
                if (indexOfCR < 0 || indexOfCR == objectToDeserialize.Length - 1)
                {
                    return null;
                }

                if (objectToDeserialize[indexOfCR + 1] == LF)
                {
                    return indexOfCR;
                }

                start = indexOfCR + 1;
            }
        }

        int? FillHeaders(int start, params HttpHeaders[] headers)
        {
            var parsedHeaders = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            int? end;
            do
            {
                end = GetNextCRLF(start);
                if (end != start)
                {
                    var indexOfHeaderSeparator = Array.IndexOf(objectToDeserialize, (byte)HeaderSeparator, start, GetCount(start, end));

                    string name;
                    string value;
                    if (indexOfHeaderSeparator < 0)
                    {
                        name = GetString(start, end);

                        value = "";
                    }
                    else
                    {
                        name = GetString(start, indexOfHeaderSeparator);
                        value = GetString(indexOfHeaderSeparator + 1, end);
                    }

                    if (!parsedHeaders.TryGetValue(name, out var headerValues))
                    {
                        headerValues = new();
                        parsedHeaders.Add(name, headerValues);
                    }

                    headerValues.Add(value.TrimStart(Spaces));

                    if (end == null)
                    {
                        break;
                    }

                    start = end.Value + CRLF.Length;
                }
            }
            while (end != null && end < start);

            foreach (var header in parsedHeaders)
            {
                foreach (var hh in headers)
                {
                    if (hh.TryAddWithoutValidation(header.Key, header.Value))
                    {
                        break;
                    }
                }
            }

            return end;
        }

        static int ToInt32(string value, string message, int fromBase = 10)
        {
            try
            {
                return Convert.ToInt32(value, fromBase);
            }
            catch (Exception e)
            {
                throw new FormatException(message, e);
            }
        }

        var statusLineEnd = GetNextCRLF(0);
        var firstLine = GetString(0, statusLineEnd);
        if (!firstLine.StartsWith(HttpResponseSignature, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new FormatException("Invalid prefix.");
        }

        var values = firstLine.Split(Spaces, 3, StringSplitOptions.RemoveEmptyEntries);

        var response = new HttpResponseMessage();
        try
        {
            if (values.Length > 0)
            {
                response.StatusCode = (HttpStatusCode)ToInt32(values[1], "Status Code is not an integer.");
            }

            try
            {
                var versionString = values[0][HttpResponseSignature.Length..];
                response.Version = versionString.Contains('.')
                    ? Version.Parse(versionString)
                    : new Version(Convert.ToInt32(versionString), 0);
            }
            catch (Exception e)
            {
                throw new FormatException("Version cannot be parsed.", e);
            }

            if (values.Length > 2)
            {
                response.ReasonPhrase = values[2];
            }

            var ms = new MemoryStream();
            var contentStream = ms;
            try
            {
                response.Content = new StreamContent(ms);
                ms = null;
            }
            finally
            {
                ms?.Dispose();
            }

            response.Content.Headers.ContentLength = null;


            var headersEnd = statusLineEnd != null ? FillHeaders(statusLineEnd.Value + CRLF.Length, response.Headers, response.Content.Headers) : null;
            if (headersEnd != null)
            {
                var contentStart = headersEnd.Value + CRLF.Length;
                int? trailingHedersStart = null;

                if (response.Headers.TransferEncodingChunked == true)
                {
                    var start = contentStart;
                    while (true)
                    {
                        var end = GetNextCRLF(start);

                        var byteCount = ToInt32(GetString(start, end), "Incorrect chunks size.", 16);
                        if (byteCount == 0)
                        {
                            if (end != null)
                            {
                                trailingHedersStart = end.Value + CRLF.Length;
                            }

                            break;
                        }

                        var blockStart = start + CRLF.Length + GetCount(start, end);

                        contentStream.Write(objectToDeserialize, blockStart, byteCount);

                        if (end == null)
                        {
                            break;
                        }

                        start = blockStart + byteCount;

                        if (GetNextCRLF(start) != start)
                        {
                            start = objectToDeserialize.Length;

                            break;
                        }

                        start += CRLF.Length;
                    }
                }
                else if (response.Content.Headers.ContentLength != null)
                {
                    var contentLength = (int)Math.Min(response.Content.Headers.ContentLength.Value, GetCount(contentStart, null));

                    contentStream.Write(objectToDeserialize, contentStart, contentLength);

                    trailingHedersStart = contentStart + contentLength;
                }
                else
                {
                    contentStream.Write(objectToDeserialize, contentStart, GetCount(contentStart, null));
                }

                if (trailingHedersStart != null)
                {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATE || NET5_0_OR_GREATER
                    FillHeaders(trailingHedersStart.Value, response.TrailingHeaders);
#else
                    FillHeaders(trailingHedersStart.Value, response.Headers, response.Content.Headers);
#endif
                }
            }

            contentStream.Position = 0;

            var res = response;
            response = null;

            return res;
        }
        finally
        {
            response?.Dispose();
        }
    }

    /// <inheritdoc/>
    public byte[] Serialize(HttpResponseMessage objectToSerialize)
    {
        Guard.Argument(objectToSerialize).NotNull();

        using var stream = new MemoryStream();

        StreamWriter CreateWriter() =>
            new(stream, Utf8, 8 * 1024, leaveOpen: true)
            {
                NewLine = CRLF
            };

        void Write(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            using var writer = CreateWriter();
            foreach (var header in headers)
            {
                foreach (var value in header.Value)
                {
                    writer.Write(header.Key);
                    writer.Write(HeaderSeparator);
                    writer.WriteLine(value);
                }
            }
        }

        void WriteLine(string? line = null)
        {
            using var writer = CreateWriter();
            writer.WriteLine(line);
        }

        using (var writer = CreateWriter())
        {
            writer.Write(HttpResponseSignature);
            writer.Write(objectToSerialize.Version.ToString(2));
            writer.Write(' ');
            writer.Write(objectToSerialize.StatusCode.ToString("d"));

            if (objectToSerialize.ReasonPhrase != null)
            {
                writer.Write(' ');
                writer.Write(objectToSerialize.ReasonPhrase);
            }
            writer.WriteLine();

        }

        Write(objectToSerialize.Headers);
        Write(objectToSerialize.Content.Headers);

        WriteLine();

        objectToSerialize.Content.LoadIntoBufferAsync().Wait();

        using (var contentStream = objectToSerialize.Content.ReadAsStreamAsync().Result)
        {
            if (objectToSerialize.Headers.TransferEncodingChunked == true)
            {
                var buffer = new byte[16 * 1024];
                while (true)
                {
                    var read = contentStream.Read(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        WriteLine(read.ToString("X", CultureInfo.InvariantCulture));

                        stream.Write(buffer, 0, read);

                        WriteLine();
                    }
                    else
                    {
                        break;
                    }
                }

                WriteLine("0");
            }
            else
            {
                contentStream.CopyTo(stream);
            }
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATE || NET5_0_OR_GREATER
        Write(objectToSerialize.TrailingHeaders);
#endif

        return stream.ToArray();
    }
}

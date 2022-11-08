// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Axion.Extensions.Configuration;

static class DictionaryExtensions
{
    public static void Add(this IDictionary<string, string> data, string? prefix, IEnumerable<KeyValuePair<string, JToken>> values)
    {
        foreach (var pair in values)
        {
            var key = string.IsNullOrEmpty(prefix) ? pair.Key : $"{prefix}:{pair.Key}";

            switch (pair.Value.Type)
            {
                case JTokenType.Null:
                    data[key] = "";
                    break;

                case JTokenType.Object:
                    data.Add(key, (IEnumerable<KeyValuePair<string, JToken>>)pair.Value);
                    break;

                case JTokenType.Array:
                    data.Add(key, (IEnumerable<JToken>)pair.Value);
                    break;

                default:
                    data[key] = pair.Value.Value<string>() ?? "";
                    break;
            }
        }
    }

    public static void Add<T>(this IDictionary<string, string> data, string? prefix, IEnumerable<T> value)
        where T: JToken =>
        data.Add(prefix, value.Select((item, position)=> new KeyValuePair<string, JToken>(position.ToString(CultureInfo.InvariantCulture), item)));
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace System;

static class StringExensions
{
    static bool IsPathSeparator(this char c) =>
        c == '/' || c == '\\' || c == Path.PathSeparator || c == Path.AltDirectorySeparatorChar;

    public static string GetParent(this string url)
    {
        var index = url.LastIndexOf('/');

        return index > 0 ? url[..index] : "";
    }

    public static string ReplaceFirst(this string url, string oldValue, string newValue)
    {
        var index = url.IndexOf(oldValue, StringComparison.Ordinal);

        return index > 0 ? string.Concat(url[..index], newValue, url[(index + oldValue.Length)..]) : url;
    }

    [return: NotNullIfNotNull(nameof(value))]
    public static string? NormalizePathSeparators(this string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }
        else
        {
            var result = new StringBuilder(value!.Length);
            var lastNotSeparator = 0;
            var lastCharIsSeparator = true; // we trim starting separators 

            foreach (var c in value)
            {
                var isSeparator = c.IsPathSeparator();
                if (isSeparator)
                {
                    if (!lastCharIsSeparator)
                    {
                        result.Append('/');
                    }
                }
                else
                {
                    result.Append(c);

                    lastNotSeparator = result.Length;
                }

                lastCharIsSeparator = isSeparator;
            }

            result.Length = lastNotSeparator;

            return result.ToString();
        }
    }
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System;
#pragma warning restore IDE0130 // Namespace does not match folder structure

static class StringExtensions
{
    extension(string line)
    {
        public string EnsureNotError()
        {
            const string ErrPrefix = "ERR ";
            if (line.StartsWith(ErrPrefix, StringComparison.Ordinal))
            {
                throw new FormatException(line[ErrPrefix.Length..]);
            }

            return line;
        }
    }
}

// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extensions for <see cref="Uri"/>.
/// </summary>
public static class UriExtensions
{
    extension(Uri)
    {

        /// <summary>
        /// Tries to create ssh <see cref="Uri"/> from the SCP string.
        /// </summary>
        /// <param name="scp">An SCP string.</param>
        /// <param name="port">The port.</param>
        /// <param name="uri">The constructed <see cref="Uri"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="scp"/> has been parsed and <see langword="false"/> otherwise.</returns>
        public static bool TryCreateFromScp(string? scp, int port, [NotNullWhen(true)] out Uri? uri)
        {
            uri = null;

            if (string.IsNullOrWhiteSpace(scp) || port <= 0 || port > ushort.MaxValue)
            {
                return false;
            }
            else
            {
                var colon = scp!.IndexOf(':');

                string Port() =>
                    port == 22
                     ? ""
                     : $":{port}";

                return colon > 0
                    && Uri.TryCreate($"ssh://{scp[..colon]}{Port()}{scp[(colon + 1)..]}", UriKind.Absolute, out uri);
            }
        }

        /// <summary>
        /// Tries to create ssh <see cref="Uri"/> from the SCP string using default port (22).
        /// </summary>
        /// <param name="scp">An SCP string.</param>
        /// <param name="uri">The constructed <see cref="Uri"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="scp"/> has been parsed and <see langword="false"/> otherwise.</returns>
        public static bool TryCreateFromScp(string? scp, [NotNullWhen(true)] out Uri? uri) =>
            TryCreateFromScp(scp, 22, out uri);

        /// <summary>
        /// Creates the ssh <see cref="Uri"/> from the SCP string.
        /// </summary>
        /// <param name="scp">An SCP string.</param>
        /// <param name="port">The port.</param>
        /// <returns>The ssh <see cref="Uri"/>.</returns>
        /// <exception cref="ArgumentNullException">Throws if the value of <paramref name="scp"/> is <see langword="null"/>.</exception>
        /// <exception cref="FormatException">Throws if the value <paramref name="scp"/> is not valid SCP string.</exception>
        public static Uri CreateFromScp(string scp, int port = 22)
        {
            ArgumentNullException.ThrowIfNull(scp);

            return TryCreateFromScp(scp, port, out var res)
                ? res
                : throw new FormatException();
        }
    }
}

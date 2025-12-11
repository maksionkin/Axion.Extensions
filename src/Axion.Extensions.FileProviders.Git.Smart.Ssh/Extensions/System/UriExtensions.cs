// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text;

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
        public static bool TryCreateFromScp(string? scp, int? port, [NotNullWhen(true)] out Uri? uri)
        {
            uri = null;

            if (string.IsNullOrWhiteSpace(scp) || (port.HasValue && (port.Value <= 0 || port.Value > ushort.MaxValue)))
            {
                return false;
            }
            else
            {
                var colon = scp!.IndexOf(':');
                if (colon > 0 && colon < scp.Length - 1)
                {
                    var sb = new StringBuilder("ssh://");
                    sb.Append(scp, 0, colon);
                    if (port.HasValue)
                    {
                        sb.Append(':')
                            .Append(port);
                    }

                    if (scp[colon + 1] != '/')
                    {
                        sb.Append('/');
                    }

                    sb.Append(scp, colon + 1, scp.Length - colon - 1);

                    return Uri.TryCreate(sb.ToString(), UriKind.Absolute, out uri);
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to create ssh <see cref="Uri"/> from the SCP string using default port (22).
        /// </summary>
        /// <param name="scp">An SCP string.</param>
        /// <param name="uri">The constructed <see cref="Uri"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="scp"/> has been parsed and <see langword="false"/> otherwise.</returns>
        public static bool TryCreateFromScp(string? scp, [NotNullWhen(true)] out Uri? uri) =>
            TryCreateFromScp(scp, null, out uri);

        /// <summary>
        /// Creates the ssh <see cref="Uri"/> from the SCP string.
        /// </summary>
        /// <param name="scp">An SCP string.</param>
        /// <param name="port">The port.</param>
        /// <returns>The ssh <see cref="Uri"/>.</returns>
        /// <exception cref="ArgumentNullException">Throws if the value of <paramref name="scp"/> is <see langword="null"/>.</exception>
        /// <exception cref="FormatException">Throws if the value <paramref name="scp"/> is not valid SCP string.</exception>
        public static Uri CreateFromScp(string scp, int? port = null)
        {
            ArgumentNullException.ThrowIfNull(scp);

            return TryCreateFromScp(scp, port, out var res)
                ? res
                : throw new FormatException();
        }
    }
}

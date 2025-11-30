// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace Axion.Extensions.FileProviders;

/// <summary>
/// Provides read-only access to files and directories in a remote Git repository over the Smart HTTP protocol, enabling
/// file enumeration and retrieval through the <see cref="IFileProvider"/> interface.
/// </summary>
/// <remarks>
/// Initializes a new instance of the GitSmartHttpFileProvider class using the specified options.
/// </remarks>
/// <param name="options">The configuration options for the <see cref="GitSmartSshFileProvider"/>. Cannot be <see langword="null"/>.</param>
/// <param name="sshClient">The <see cref="ISshClient"/> to connect to the repository.</param>
public class GitSmartSshFileProvider(IOptions<GitFileProviderOptions> options, ISshClient sshClient) : GitSmartFileProvider(options, "ssh")
{
    readonly bool ownsSshClient;

    readonly ISshClient sshClient = sshClient ?? throw new ArgumentNullException(nameof(sshClient));

    /// <summary>
    /// Initializes a new instance of the GitSmartSshFileProvider class using the specified options and a collection of
    /// private key sources for SSH authentication.
    /// </summary>
    /// <remarks>This constructor creates and manages its own SSH client instance based on the provided
    /// repository options and private keys. The SSH client will be disposed when the file provider is
    /// disposed.</remarks>
    /// <param name="options">The options used to configure the Git file provider. Cannot be <see langword="null"/>.</param>
    /// <param name="keys">A collection of private key sources used for SSH authentication. Cannot be <see langword="null"/> or empty.</param>
    public GitSmartSshFileProvider(IOptions<GitFileProviderOptions> options, IEnumerable<IPrivateKeySource> keys) : this(options, CreateSshClient(options.Value.Repository, keys)) =>
        ownsSshClient = true;

    /// <inheritdoc/>
    protected override async ValueTask<Stream> GetInfoRefsAsync(CancellationToken cancellationToken = default) =>
        await RunAsync(default, cancellationToken);

    /// <inheritdoc/>
    protected override async ValueTask<Stream> GetObjectsAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default) =>
                await RunAsync(payload, cancellationToken);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && ownsSshClient)
        {
            sshClient.Dispose();
        }

        base.Dispose(disposing);
    }

    static SshClient CreateSshClient(Uri repository, IEnumerable<IPrivateKeySource> keyFiles)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentOutOfRangeException.ThrowIfNotEqual(repository.IsAbsoluteUri, true);
        ArgumentNullException.ThrowIfNull(keyFiles);
        ArgumentOutOfRangeException.ThrowIfEqual(keyFiles.Any(), false);

        return new(repository.Host, repository.IsDefaultPort ? 22 : repository.Port, repository.UserInfo.Split(':', 2).First(), [.. keyFiles]);
    }
    async ValueTask<Stream> RunAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        var repository = Repository;

        if (!sshClient.IsConnected)
        {
            await sshClient.ConnectAsync(cancellationToken);
        }

        SshCommand? command = null;
        IAsyncResult? task = null;

        try
        {
            command = sshClient.CreateCommand($"git-upload-pack {repository.AbsolutePath}");
            task = command.BeginExecute();
            using (var stream = command.CreateInputStream())
            {
                if (payload.IsEmpty)
                {
                    await stream.WriteAsync(payload, cancellationToken);
                }
            }

            var res = new OutputStream(command, task);

            command = null;
            task = null;

            return res;
        }
        finally
        {
            if (task != null)
            {
                command?.EndExecute(task);
            }

            command?.Dispose();
        }
    }
}

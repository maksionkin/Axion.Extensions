using System.Text;
using Axion.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders;
using Renci.SshNet;

namespace Axion.Extensions.FileProviders.Git.Smart.Ssh.Tests;

[TestClass]
public class GitSmartSshFileProviderTests
{
    public required TestContext TestContext { get; init; }
    
    [TestMethod]
    public void CheckScpWithoutSlash()
    {
        Assert.AreEqual(new Uri("ssh://git@github.com/maksionkin/Axion.Extensions"), UriExtensions.CreateFromScp("git@github.com:maksionkin/Axion.Extensions"));
    }

    [TestMethod]
    public void CheckScpWithSlash()
    {
        Assert.AreEqual(new Uri("ssh://git@github.com/maksionkin/Axion.Extensions"), UriExtensions.CreateFromScp("git@github.com:/maksionkin/Axion.Extensions"));
    }

    [TestMethod]
    public void CheckScpWithSlashAndPort()
    {
        Assert.AreEqual(new Uri("ssh://git@github.com:22/maksionkin/Axion.Extensions"), UriExtensions.CreateFromScp("git@github.com:/maksionkin/Axion.Extensions", 22));
    }

    [TestMethod]
    public void CheckAllDeep()
    {
        var root = Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!, "gh");
        var key = new UTF8Encoding(false, false).GetBytes(Environment.GetEnvironmentVariable("SSHTESTKEY")!);

        using var physicalProvider = new PhysicalFileProvider(root, Microsoft.Extensions.FileProviders.Physical.ExclusionFilters.None);

        using var keyStream = new MemoryStream(key);
        using var gitProvider = new GitSmartSshFileProvider(
            new GitFileProviderOptions() { Repository = Uri.CreateFromScp("git@github.com:/maksionkin/Axion.Extensions") },
            [new PrivateKeyFile(keyStream)]
        );

        var toProcess = new Stack<string>();
        toProcess.Push("");

        while (toProcess.Count > 0)
        {
            var subpath = toProcess.Pop();

            TestContext.WriteLine($"Processing [{subpath}].");

            var phisycals = physicalProvider.GetDirectoryContents(subpath).ToDictionary(file => file.Name);

            foreach (var gitHubItem in gitProvider.GetDirectoryContents(subpath))
            {
                Assert.IsTrue(gitHubItem.Exists);

                if (phisycals.TryGetValue(gitHubItem.Name, out var physicalItem))
                {
                    Assert.AreEqual(physicalItem.IsDirectory, gitHubItem.IsDirectory);

                    Assert.AreEqual(physicalItem.Length, gitHubItem.Length);
                    if (gitHubItem.IsDirectory)
                    {
                        toProcess.Push(Path.Combine(subpath, gitHubItem.Name));
                    }
                    else
                    {
                        using var stream = gitHubItem.CreateReadStream();
                        var buffer = new byte[4096];
                        var length = 0L;
                        while (true)
                        {
                            var read = stream.Read(buffer, 0, buffer.Length);
                            length += read;

                            if (read <= 0)
                            {
                                break;
                            }
                        }

                        Assert.AreEqual(gitHubItem.Length, length);
                    }
                }
            }
        }
    }
}

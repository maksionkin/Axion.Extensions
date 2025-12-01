using Axion.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders;

namespace Axion.Extensions.FileProviders.Git.Smart.Http.Tests;

[TestClass]
public class GitSmartHttpFileProviderTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public void CheckAllDeep()
    {
        var root = Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!, "gh");

        using var physicalProvider = new PhysicalFileProvider(root, Microsoft.Extensions.FileProviders.Physical.ExclusionFilters.None);

        using var gitProvider = new GitSmartHttpFileProvider(new GitFileProviderOptions() { Repository = new("https://github.com/maksionkin/Axion.Extensions") });

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

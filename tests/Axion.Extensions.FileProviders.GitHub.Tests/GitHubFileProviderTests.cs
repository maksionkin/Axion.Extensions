using Microsoft.Extensions.FileProviders;

namespace Axion.Extensions.FileProviders.GitHub.Tests;

[TestClass]
public class GitHubFileProviderTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public void NotFoundRepo()
    {
        var provider = new GitHubFileProvider(new GitHubFileProviderOptions { Owner = "maksionkin", Name = "not-found" });

        var contents = provider.GetDirectoryContents("/");

        Assert.IsFalse(contents.Exists);
        Assert.AreEqual(contents.Count(), 0);
    }

    [TestMethod]
    public void CheckAllDeep() =>
        CheckAllDeep(false);

    [TestMethod]
    public void CheckAllDeepWithoutLastModified() =>
        CheckAllDeep(true);

    [TestMethod]
    public void CheckLastModified()
    {
        var gitHubProvider = new GitHubFileProvider(new GitHubFileProviderOptions
        {
            Owner = "maksionkin",
            Name = "Axion.Extensions",
            Credentials = new(Environment.GetEnvironmentVariable("GITHUBTOKEN"))
        });

        using var textStream = File.OpenText(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!, "gh-files.txt"));
        while (!textStream.EndOfStream)
        {
            var line = textStream.ReadLine();
            if (!string.IsNullOrEmpty(line))
            {
                var a = line.Split(['\t'], 2);

                var subpath = a[1];
                var lastModified = DateTimeOffset.Parse(a[0]).ToUniversalTime();

                TestContext.WriteLine($"Processing [{subpath}].");

                var item = gitHubProvider.GetFileInfo(a[1]);

                Assert.IsTrue(item.Exists);
                Assert.IsFalse(item.IsDirectory);

                Assert.AreEqual(lastModified, item.LastModified.ToUniversalTime());
            }
        }
    }

    void CheckAllDeep(bool skipLastModified)
    {
        var root = Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!, "gh");

        using var physicalProvider = new PhysicalFileProvider(root, Microsoft.Extensions.FileProviders.Physical.ExclusionFilters.None);

        var gitHubProvider = new GitHubFileProvider(new GitHubFileProviderOptions
        {
            Owner = "maksionkin",
            Name = "Axion.Extensions",
            Credentials = new(Environment.GetEnvironmentVariable("GITHUBTOKEN")),
            SkipLoadingLastModified = skipLastModified
        });

        var toProcess = new Stack<string>();
        toProcess.Push("");

        while (toProcess.Count > 0)
        {
            var subpath = toProcess.Pop();

            TestContext.WriteLine($"Processing [{subpath}].");

            var phisycals = physicalProvider.GetDirectoryContents(subpath).ToDictionary(file => file.Name);

            foreach (var gitHubItem in gitHubProvider.GetDirectoryContents(subpath))
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

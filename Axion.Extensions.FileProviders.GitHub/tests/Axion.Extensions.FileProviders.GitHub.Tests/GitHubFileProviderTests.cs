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
    public void ContentAllCheck()
    {
        var root = GetType().Assembly.Location;
        for (var i = 0; i < 7; i++)
        {
            root = Path.GetDirectoryName(root)!;
        }

        throw new Exception(root);

        var physicalProvider = new PhysicalFileProvider(root, Microsoft.Extensions.FileProviders.Physical.ExclusionFilters.None);

        var gitHubProvider = new GitHubFileProvider(new GitHubFileProviderOptions 
        { 
            Owner = "maksionkin", 
            Name = "Axion.Extensions",
            Credentials = new(Environment.GetEnvironmentVariable("GITHUBTOKEN")) 
        });

        var toProcess = new Stack<string>();
        toProcess.Push("");

        while (toProcess.Count > 0)
        {
            var subpath = toProcess.Pop();

            TestContext.WriteLine($"Processing {subpath}.");
         
            var phisycals = physicalProvider.GetDirectoryContents(subpath).ToDictionary(file => file.Name);

            foreach (var gitHubItem in gitHubProvider.GetDirectoryContents(subpath))
            {
                Assert.IsTrue(gitHubItem.Exists);

                var physicalItem = phisycals[gitHubItem.Name];

                Assert.AreEqual(physicalItem.IsDirectory, gitHubItem.IsDirectory);

                //Assert.AreEqual(physicalItem.Length, gitHubItem.Length);
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
                        var read = stream.Read(buffer);
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

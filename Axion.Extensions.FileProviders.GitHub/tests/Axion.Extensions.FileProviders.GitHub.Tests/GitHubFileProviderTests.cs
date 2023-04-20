using Microsoft.Extensions.FileProviders;

namespace Axion.Extensions.FileProviders.GitHub.Tests;

[TestClass]
public class GitHubFileProviderTests
{
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

        var physicalProvider = new PhysicalFileProvider(root, Microsoft.Extensions.FileProviders.Physical.ExclusionFilters.None);
        var gitHubProvider = new GitHubFileProvider(new GitHubFileProviderOptions { Owner = "maksionkin", Name = "Axion.Extensions" });

        void Compare(string subpath)
        {
            var phisycals = physicalProvider.GetDirectoryContents(subpath).ToDictionary(file => file.Name);

            foreach (var gitHubItem in gitHubProvider.GetDirectoryContents(subpath))
            {
                Assert.IsTrue(gitHubItem.Exists);

                var physicalItem = phisycals[gitHubItem.Name];

                Assert.AreEqual(physicalItem.IsDirectory, gitHubItem.IsDirectory);

                Assert.AreEqual(physicalItem.Length, gitHubItem.Length);
                if (gitHubItem.IsDirectory)
                {
                    Compare(Path.Combine(subpath, gitHubItem.Name));
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

        Compare("");
/*
        var d = gitHubProvider.GetFileInfo("");

        var content = gitHubProvider.GetDirectoryContents("");


        foreach (var f in content)//.Zip(Directory.GetFiles(Path.Combine(GetType().Assembly.Location, "..", "..", "..", "..", "..", "..", ".."))))
        {
            f.LastModified.ToString();
            if (!f.IsDirectory)
            {
                f.CreateReadStream().Close();
            }
        }*/
    }
}

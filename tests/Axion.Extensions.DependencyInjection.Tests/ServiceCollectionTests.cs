using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;

namespace Axion.Extensions.FileProviders.GitHub.Tests;

[TestClass]
public class ServiceCollectionTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public void RequiredAndNullalleMissingParameters()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton(TestContext);
        collection.AddSingleton(new TypeForwardedFromAttribute("0"));

        collection.AddTransient((TestContext context, IEnumerable<TypeForwardedFromAttribute> from, OptionalAttribute? optional) =>
        {
            Assert.IsNotNull(from);
            Assert.AreEqual(from.Count(), 1);
            Assert.AreEqual(from.First().AssemblyFullName,"0");

            Assert.AreEqual(TestContext, context);
            Assert.IsNull(optional);

            return Tuple.Create(context, optional);
        });

        using var provider = collection.BuildServiceProvider();
        var tuple = provider.GetRequiredService<Tuple<TestContext, OptionalAttribute?>>();

        Assert.IsNotNull(tuple);
        Assert.IsNotNull(tuple.Item1);
        Assert.IsNull(tuple.Item2);
    }

    [TestMethod]
    public void RequiredAndNullalleExistsParameters()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton(TestContext);
        collection.AddScoped<OptionalAttribute>();

        collection.AddTransient((TestContext context, OptionalAttribute? optional, TypeForwardedFromAttribute? from) =>
        {
            Assert.AreEqual(TestContext, context);
            Assert.IsNotNull(optional);
            Assert.IsNull(from);

            return Tuple.Create(context, optional);
        });

        using var provider = collection.BuildServiceProvider();
        var tuple = provider.GetRequiredService<Tuple<TestContext, OptionalAttribute?>>();

        Assert.IsNotNull(tuple);
        Assert.IsNotNull(tuple.Item1);
        Assert.IsNotNull(tuple.Item2);
    }

    [TestMethod]
    public void RequiredMissingParameters()
    {
        var collection = new ServiceCollection();

        collection.AddTransient((TestContext context, OptionalAttribute? optional) =>
        {
            Assert.Fail();

            return Tuple.Create(context, optional);
        });

        using var provider = collection.BuildServiceProvider();

        Assert.ThrowsException<InvalidOperationException>(provider.GetRequiredService<Tuple<TestContext, OptionalAttribute?>>);
    }
}

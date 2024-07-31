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
        collection.AddKeyedSingleton("key", new FromKeyedServicesAttribute(0));

        collection.AddTransient((TestContext context, [FromKeyedServices("key")] IEnumerable<FromKeyedServicesAttribute> from, OptionalAttribute? optional) =>
        {
            Assert.IsNotNull(from);
            Assert.AreEqual(from.Count(), 1);
            Assert.AreEqual(from.First().Key, 0);

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

        collection.AddTransient((TestContext context, OptionalAttribute? optional, FromKeyedServicesAttribute? from) =>
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
    public void KeyedParamCheckParameters()
    {
        var collection = new ServiceCollection();
        var key = "test";

        collection.AddSingleton(TestContext);
        collection.AddKeyedTransient(key, (TestContext context, object? k) =>
        {
            Assert.AreEqual(TestContext, context);
            Assert.AreEqual(k, key);

            return Tuple.Create(context, k);
        });

        using var provider = collection.BuildServiceProvider();
        var tuple = provider.GetRequiredKeyedService<Tuple<TestContext, object>>(key);

        Assert.IsNotNull(tuple);
        Assert.IsNotNull(tuple.Item1);
        Assert.IsNotNull(tuple.Item2);
        Assert.AreEqual(tuple.Item2, key);
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

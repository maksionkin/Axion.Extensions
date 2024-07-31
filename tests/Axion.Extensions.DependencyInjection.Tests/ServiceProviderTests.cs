using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;

namespace Axion.Extensions.FileProviders.GitHub.Tests;

[TestClass]
public class ServiceProvderTests
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

        var called = false;

        using var provider = collection.BuildServiceProvider();
        var tuple = provider.Execute((Tuple<TestContext, OptionalAttribute?> t) => { called = true; return t; });

        Assert.IsTrue(called);
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

            return Tuple.Create(context, optional, from);
        });

        using var provider = collection.BuildServiceProvider();
        var tuple = provider.Execute((Tuple<TestContext, OptionalAttribute?, FromKeyedServicesAttribute?> t) => t);

        Assert.IsNotNull(tuple);
        Assert.IsNotNull(tuple.Item1);
        Assert.IsNotNull(tuple.Item2);
        Assert.IsNull(tuple.Item3);
    }

    [TestMethod]
    public void KeyedParamCheckParameters()
    {
        var collection = new ServiceCollection();
        const string key = "test";

        collection.AddSingleton(TestContext);
        collection.AddKeyedTransient(key, (TestContext context, object? k) =>
        {
            Assert.AreEqual(TestContext, context);
            Assert.AreEqual(k, key);

            return Tuple.Create(context, k);
        });

        using var provider = collection.BuildServiceProvider();
        var tuple = provider.Execute(([FromKeyedServices(key)] Tuple<TestContext, object> t) => t);

        Assert.IsNotNull(tuple);
        Assert.IsNotNull(tuple.Item1);
        Assert.IsNotNull(tuple.Item2);
        Assert.AreEqual(tuple.Item2, key);
    }

    [TestMethod]
    public void ValueTypeCheckParameters()
    {
        const int i = 13;

        var collection = new ServiceCollection();
        collection.AddSingleton(TestContext);


        using var provider = collection.BuildServiceProvider();

        var res = provider.Execute((TestContext t) => i);

        Assert.AreEqual(i, res);
    }

    [TestMethod]
    public void TaskCheckParameters()
    {
        const int i = 13;

        var collection = new ServiceCollection();
        collection.AddSingleton(TestContext);


        using var provider = collection.BuildServiceProvider();

        var res = provider.ExecuteAsync(async (TestContext t) => await Task.FromResult(i)).Result;

        Assert.AreEqual(i, res);
    }
}

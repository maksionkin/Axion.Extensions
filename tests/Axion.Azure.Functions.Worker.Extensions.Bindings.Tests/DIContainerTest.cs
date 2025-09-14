using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Axion.Azure.Functions.Worker.Extensions.OutputBindings.Tests;

[TestClass]
public class DIContainerTest
{
    [TestMethod]
    public async Task EnsureRequiredServices()
    {
        // Arrange
        var provider = CreateServiceProvider();

        // Assert
        Assert.IsNotNull(provider.GetService<IBinder>());

        Assert.IsNotNull(provider.GetService<IFunctionContextAccessor>());

        Assert.IsNotNull(provider.GetService<IAsyncConverter<string, BinaryData>>());

        Assert.AreEqual("test", (await provider.GetRequiredService<IAsyncConverter<string, BinaryData>>().ConvertAsync("test", default)).ToString());

        var guid = Guid.NewGuid();
        Assert.AreEqual(guid.ToString(), await provider.GetRequiredService<IAsyncConverter<Guid, string>>().ConvertAsync(guid, default));

        var converter = provider.GetRequiredService<IAsyncConverter<TestData, string>>();
        var j = await converter.ConvertAsync(new TestData("a", 1), default);

        Assert.AreEqual("{\"Name\":\"a\",\"Age\":1}", j);
    }

    [TestMethod]
    public async Task CheckTypeConverterForAttribute()
    {
        // Arrange
        var provider = CreateServiceProvider();

        // Act
        var binder = provider.GetRequiredService<IBinder>();

        Assert.AreEqual(6, await binder.BindAsync<int>(new TestAttribute(6)));

        Assert.AreEqual("13", await binder.BindAsync<string>(new TestAttribute(13)));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await binder.BindAsync<Guid>(new TestAttribute(1)));
    }

    [TestMethod]
    public async Task CheckTypeConverterForCollectionAttribute()
    {
        // Arrange
        var provider = CreateServiceProvider();

        // Act
        var binder = provider.GetRequiredService<IBinder>();

        Assert.IsTrue((await binder.BindAsync<IEnumerable<int>>(new TestEnumerableAttribute(6, 13))).SequenceEqual([6, 13]));

        Assert.IsTrue(await (await binder.BindAsync<IAsyncEnumerable<string>>(new TestEnumerableAttribute(6, 13))).SequenceEqualAsync(new[] { "6", "13" }.ToAsyncEnumerable()));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await binder.BindAsync<Guid>(new TestAttribute(1)));
    }
    static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddFunctionsWorkerCore();
        services.AddWorkerBinding();

        services.Remove(services.First(sd => sd.ServiceType.FullName == "Microsoft.Azure.Functions.Worker.IFunctionsApplication"));
        services.Remove(services.First(sd => sd.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)));

        return services.BuildServiceProvider(new ServiceProviderOptions() { ValidateScopes = true, ValidateOnBuild = true });
    }

    record TestData(string Name, int Age);

    class TestAttribute(int i) : BindingAttribute([typeof(int)])
    {
        protected override ValueTask<object> BindAsync(IServiceProvider serviceProvider, Type type, CancellationToken cancellationToken)
        {
            return new(i);
        }
    }
    class TestEnumerableAttribute(params IEnumerable<int> i) : BindingAttribute([typeof(IEnumerable<int>)])
    {
        protected override ValueTask<object> BindAsync(IServiceProvider serviceProvider, Type type, CancellationToken cancellationToken)
        {
            return new(i);
        }
    }
}


// Copyright (c) Michael Aksionkin. All rights reserved.
// Licensed under the MIT License

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Axion.Azure.Functions.Worker;
using Axion.Azure.Functions.Worker.Converters;
using Axion.Azure.Functions.Worker.Converters.Providers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.Azure.WebJobs;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for registering output binding services in an <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>This class contains methods to configure and register services required for output binding
/// functionality in worker-based Azure Functions. These services include context accessors, parameter binders, and
/// collectors for handling output bindings.</remarks>
public static class BindingServiceCollectionExtensions
{
    /// <summary>
    /// Adds the necessary services for output binding to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services"> The <see cref="IServiceCollection"/> to add the output binding services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> with the output binding services added.</returns>
    public static IServiceCollection AddWorkerBinding(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAsyncConverterProvider, SameTypeConverterProvider>();
        services.AddSingleton<IAsyncConverterProvider, AsyncConverterProvider>();
        services.AddSingleton<IAsyncConverterProvider, SyncConverterProvider>();
        services.AddSingleton<IAsyncConverterProvider, SubTypeConverterProvider>();
        services.AddSingleton<IAsyncConverterProvider, CollectorConverterProvider>();
        services.AddSingleton<IAsyncConverterProvider, EnumerationConverterProvider>();
        services.AddSingleton<IAsyncConverterProvider, ObjectToBaseTypeConverterProvider>();
        services.AddSingleton<IAsyncConverterProvider, BaseTypeToObjectConverterProvider>();

        services.RegisterSimpleTypesConverters();

        services.AddConverter((BinaryData input) => input.ToString());
        services.AddConverter((BinaryData input) => input.ToArray());
        services.AddConverter((BinaryData input) => input.ToStream());
        services.AddConverter((BinaryData input) => input.ToMemory());

        services.AddConverter((string input) => BinaryData.FromString(input));
        services.AddConverter((string input) => BinaryData.FromString(input).ToArray());
        services.AddConverter((string input) => BinaryData.FromString(input).ToMemory());
        services.AddConverter((string input) => BinaryData.FromString(input).ToStream());

        services.AddConverter((byte[] input) => BinaryData.FromBytes(input));
        services.AddConverter((byte[] input) => BinaryData.FromBytes(input).ToStream());
        services.AddConverter((byte[] input) => BinaryData.FromBytes(input).ToString());
        services.AddConverter((byte[] input) => BinaryData.FromBytes(input).ToMemory());

        services.AddConverter((ReadOnlyMemory<byte> input) => BinaryData.FromBytes(input));
        services.AddConverter((ReadOnlyMemory<byte> input) => BinaryData.FromBytes(input).ToStream());
        services.AddConverter((ReadOnlyMemory<byte> input) => BinaryData.FromBytes(input).ToString());
        services.AddConverter((ReadOnlyMemory<byte> input) => BinaryData.FromBytes(input).ToArray());

        services.AddAsyncConverter(async (Stream input, CancellationToken cancellationToken) => await BinaryData.FromStreamAsync(input, cancellationToken));
        services.AddAsyncConverter(async (Stream input, CancellationToken cancellationToken) => (await BinaryData.FromStreamAsync(input, cancellationToken)).ToArray());
        services.AddAsyncConverter(async (Stream input, CancellationToken cancellationToken) => (await BinaryData.FromStreamAsync(input, cancellationToken)).ToMemory());
        services.AddAsyncConverter(async (Stream input, CancellationToken cancellationToken) => (await BinaryData.FromStreamAsync(input, cancellationToken)).ToString());

        services.AddConverter((Guid input) => input.ToString());
        services.AddConverter((string input) => Guid.ParseExact(input, "G"));

        services.AddConverter((DateTime input) => input.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        services.AddConverter((string input) => DateTime.ParseExact(input, "O", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));

        services.AddConverter((DateTimeOffset input) => input.ToString("O", CultureInfo.InvariantCulture));
        services.AddConverter((string input) => DateTimeOffset.ParseExact(input, "O", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal));

#if NET5_0_OR_GREATER
        services.AddConverter((Half input) => input.ToString(CultureInfo.InvariantCulture));
        services.AddConverter((string input) => Half.Parse(input, NumberStyles.AllowTrailingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture));
#endif

#if NET6_0_OR_GREATER
        services.AddConverter((DateOnly input) => input.ToString("O", CultureInfo.InvariantCulture));
        services.AddConverter((string input) => DateOnly.ParseExact(input, "O", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal));
#endif

#if NET7_0_OR_GREATER
        services.AddConverter((Int128 input) => input.ToString(CultureInfo.InvariantCulture));
        services.AddConverter((UInt128 input) => input.ToString(CultureInfo.InvariantCulture));
        services.AddConverter((string input) => Int128.Parse(input, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
        services.AddConverter((string input) => UInt128.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture));
#endif

        services.AddSingleton<IFunctionContextAccessor, FunctionContextAccessor>();
        services.AddSingleton<IParameterBinder, Binder>();
        services.AddSingleton<ITypeBinder>(provider => provider.GetRequiredService<IParameterBinder>());
        services.AddSingleton<IBinder>(provider => provider.GetRequiredService<IParameterBinder>());

        void ReplaceService<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            var descriptor = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(TService))
                ?? throw new InvalidOperationException($"No instance of {typeof(TService).Name} found.");

            var createPrevious = descriptor.ImplementationFactory;
            createPrevious ??= serviceProvider => ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!);

            services.Remove(descriptor);

            services.Add(new(descriptor.ServiceType,
                serviceProvider => ActivatorUtilities.CreateInstance<TImplementation>(serviceProvider, createPrevious(serviceProvider)),
                descriptor.Lifetime));
        }

        ReplaceService<IFunctionActivator, FunctionActivator>();
        ReplaceService<IFunctionExecutor, FunctionExecutor>();


        return services;
    }

    /// <summary>
    /// Adds an <see cref="IConverter{TInput, TOutput}"/> to the service collection that uses the specified delegate to perform conversions between the input and output types.
    /// </summary>
    /// <typeparam name="TInput">The input <see cref="Type"/>.</typeparam>
    /// <typeparam name="TOutput">The output <see cref="Type"/>.</typeparam>
    /// <param name="services"> The <see cref="IServiceCollection"/> to add the output binding services to.</param>
    /// <param name="convert">The delegate used to perform the conversion.</param>
    /// <returns>The <see cref="IServiceCollection"/> with the output binding services added.</returns>
    public static IServiceCollection AddConverter<TInput, TOutput>(this IServiceCollection services, Func<TInput, TOutput> convert)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(convert);

        return services.AddSingleton<IConverter<TInput, TOutput>>(new DelegateConverter<TInput, TOutput>(convert));
    }

    /// <summary>
    /// Adds an <see cref="IAsyncConverter{TInput, TOutput}"/> to the service collection that uses the specified delegate to perform conversions between the input and output types.
    /// </summary>
    /// <typeparam name="TInput">The input <see cref="Type"/>.</typeparam>
    /// <typeparam name="TOutput">The output <see cref="Type"/>.</typeparam>
    /// <param name="services"> The <see cref="IServiceCollection"/> to add the output binding services to.</param>
    /// <param name="convert">The delegate used to perform the conversion.</param>
    /// <returns>The <see cref="IServiceCollection"/> with the output binding services added.</returns>
    public static IServiceCollection AddAsyncConverter<TInput, TOutput>(this IServiceCollection services, Func<TInput, CancellationToken, ValueTask<TOutput>> convert)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(convert);

        return services.AddSingleton<IAsyncConverter<TInput, TOutput>>(new DelegateAsyncConverter<TInput, TOutput>(convert));
    }
}

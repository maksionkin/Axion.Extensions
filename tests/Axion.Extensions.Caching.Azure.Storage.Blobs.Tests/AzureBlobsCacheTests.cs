﻿using Microsoft.Extensions.Caching.Distributed;

namespace Axion.Extensions.Caching.Azure.Storage.Blobs.Tests;

[TestClass]
public class AzureBlobsCacheTests
{
    static AzureBlobsCache GetCache() =>
        new(new AzureBlobsCacheOptions { ConnectionString = "UseDevelopmentStorage=true", ExpiredItemsDeletionInterval = TimeSpan.FromMinutes(2) });

    [TestMethod]
    public async Task ReturnsNullValue_ForNonExistingCacheItem()
    {
        // Arrange
        using var cache = GetCache();

        // Act
        var value = await cache.GetAsync("NonExisting");

        // Assert
        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task SetCacheItem_SucceedsFor_VeryLongKey()
    {
        // Arrange
        // Create a key with the maximum allowed key length. Here a key of length 898 bytes is created.
        var key = new string('a', 13613);
        var expectedValue = "Hello, World!";

        using var cache = GetCache();

        // Act
        await cache.SetStringAsync(
            key, expectedValue,
            new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));

        // Assert
        var cacheItem = await cache.GetStringAsync(key);
        Assert.AreEqual(expectedValue, cacheItem);

        // Act
        await cache.RemoveAsync(key);

        // Assert
        var cacheItemInfo = await cache.GetAsync(key);
        Assert.IsNull(cacheItemInfo);
    }

    [TestMethod]
    public async Task SetCacheItem_SucceedsFor_NullAbsoluteAndSlidingExpirationTimes()
    {
        // Arrange
        var key = Guid.NewGuid().ToString();
        var expectedValue = "Hello, World!";
        var cache = GetCache();

        // Act
        await cache.SetStringAsync(key, expectedValue, new DistributedCacheEntryOptions()
        {
            AbsoluteExpiration = null,
            AbsoluteExpirationRelativeToNow = null,
            SlidingExpiration = null
        });

    }

    [TestMethod]
    public async Task SetWithSlidingExpiration_ReturnsNullValue_ForExpiredCacheItem()
    {
        var slidingExpirationWindow = TimeSpan.FromSeconds(6);

        var key = Guid.NewGuid().ToString();
        using var cache = GetCache();

        // Arrange
        var cacheItem = await cache.GetAsync(key);
        await cache.SetStringAsync(
            key,
            "Hello, World!",
            new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpirationWindow));

        await Task.Delay(slidingExpirationWindow);

        // Act
        var value = await cache.GetAsync(key);

        // Assert
        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task SetWithSlidingExpiration_ExtendsExpirationTime()
    {
        var slidingExpirationWindow = TimeSpan.FromSeconds(6);

        var expectedValue = "Hello, World!";

        var key = Guid.NewGuid().ToString();
        using var cache = GetCache();

        // Arrange
        var cacheItem = await cache.GetAsync(key);
        await cache.SetStringAsync(
            key,
            expectedValue,
            new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpirationWindow));

        await Task.Delay(slidingExpirationWindow / 2);

        // Act
        var value = await cache.GetStringAsync(key);

        await Task.Delay(slidingExpirationWindow / 2);

        await cache.RefreshAsync(key);

        await Task.Delay(slidingExpirationWindow / 2);

        value = await cache.GetStringAsync(key);

        // Assert
        Assert.AreEqual(expectedValue, value);

        await Task.Delay(slidingExpirationWindow);

        Assert.IsNull(await cache.GetAsync(key));
    }

    [TestMethod]
    public async Task GetItem_SlidingExpirationDoesNot_ExceedAbsoluteExpirationIfSet()
    {
        var slidingExpirationWindow = TimeSpan.FromSeconds(6);

        var expectedValue = "Hello, World!";

        var key = Guid.NewGuid().ToString();
        using var cache = GetCache();

        // Arrange
        var cacheItem = await cache.GetAsync(key);
        await cache.SetStringAsync(
            key,
            expectedValue,
            new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpirationWindow)
            .SetAbsoluteExpiration(DateTimeOffset.UtcNow + 1.5 * slidingExpirationWindow));

        await Task.Delay(slidingExpirationWindow / 2);

        // Act
        var value = await cache.GetStringAsync(key);

        await Task.Delay(slidingExpirationWindow / 2);

        await cache.RefreshAsync(key);

        await Task.Delay(slidingExpirationWindow / 2);

        Assert.IsNull(await cache.GetAsync(key));
    }

    [TestMethod]
    public async Task GetItem_DotSuffix()
    {
        var slidingExpirationWindow = TimeSpan.FromSeconds(6);

        var expectedValue = "Hello, World!";

        var key = Guid.NewGuid().ToString();
        var keyWithDot = key + '.';

        using var cache = GetCache();

        // Arrange
        await cache.SetStringAsync(
            keyWithDot,
            expectedValue,
            new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpirationWindow));

        // Act
        Assert.IsNull(await cache.GetAsync(key));

        var value = await cache.GetStringAsync(keyWithDot);

        Assert.AreEqual(expectedValue, value);
    }
}

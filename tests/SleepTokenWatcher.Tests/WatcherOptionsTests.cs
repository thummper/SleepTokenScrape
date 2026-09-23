using SleepTokenWatcher.Configuration;
using Xunit;

namespace SleepTokenWatcher.Tests;

public class WatcherOptionsTests
{
    [Fact]
    public void Accepts_distinct_valid_stores()
    {
        var options = Options(
            Store("sleep-token", StorePlatform.Shopify, stateFileName: "state.json"),
            Store("york-ghost-merchants", StorePlatform.Squarespace));

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Rejects_two_stores_with_the_same_key()
    {
        var options = Options(Store("shop", StorePlatform.Shopify), Store("shop", StorePlatform.Squarespace));

        Assert.Contains(options.Validate(), e => e.Contains("used more than once"));
    }

    [Fact]
    public void Rejects_two_stores_sharing_a_state_file()
    {
        var options = Options(
            Store("a", StorePlatform.Shopify, stateFileName: "b.json"),
            Store("b", StorePlatform.Squarespace));

        Assert.Contains(options.Validate(), e => e.Contains("shared by more than one store"));
    }

    [Fact]
    public void Rejects_a_key_that_is_not_file_name_safe()
    {
        var options = Options(Store("York Ghost", StorePlatform.Squarespace));

        Assert.Contains(options.Validate(), e => e.StartsWith("Store 'York Ghost'"));
    }

    [Theory]
    [InlineData(StorePlatform.Shopify, "all-products", "https://example.com/collections/all-products")]
    [InlineData(StorePlatform.Squarespace, "shop", "https://example.com/shop")]
    public void Builds_the_collection_url_for_each_platform(StorePlatform platform, string handle, string expected)
    {
        var store = Store("x", platform);
        store.CollectionHandle = handle;

        Assert.Equal(expected, store.CollectionUrl);
    }

    private static WatcherOptions Options(params StoreOptions[] stores) => new() { Stores = [.. stores] };

    private static StoreOptions Store(string key, StorePlatform platform, string? stateFileName = null) =>
        new()
        {
            Key = key,
            Name = key,
            Platform = platform,
            BaseUrl = "https://example.com/",
            CollectionHandle = "shop",
            StateFileName = stateFileName,
        };
}

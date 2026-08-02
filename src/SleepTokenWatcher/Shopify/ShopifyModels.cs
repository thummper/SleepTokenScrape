namespace SleepTokenWatcher.Shopify;

/// <summary>Raw shape of Shopify's public products.json feed. Snake-case keys are mapped by naming policy.</summary>
internal sealed class ShopifyProductsResponse
{
    public List<ShopifyProduct> Products { get; set; } = [];
}

internal sealed class ShopifyProduct
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string? ProductType { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public List<ShopifyVariant> Variants { get; set; } = [];
    public List<ShopifyImage> Images { get; set; } = [];
}

internal sealed class ShopifyVariant
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Sku { get; set; }

    /// <summary>Shopify sends money as an invariant-culture string, e.g. "25.00".</summary>
    public string? Price { get; set; }

    public bool Available { get; set; }
}

internal sealed class ShopifyImage
{
    public string? Src { get; set; }
    public int Position { get; set; }
}

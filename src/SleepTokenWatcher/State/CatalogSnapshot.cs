namespace SleepTokenWatcher.State;

/// <summary>
/// The catalogue as it looked at one point in time. Persisted between runs and diffed against the next fetch.
/// </summary>
public sealed class CatalogSnapshot
{
    public DateTimeOffset CapturedAtUtc { get; set; }

    /// <summary>Keyed by Shopify product id.</summary>
    public Dictionary<long, ProductSnapshot> Products { get; set; } = [];
}

public sealed class ProductSnapshot
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string? ProductType { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>Keyed by Shopify variant id.</summary>
    public Dictionary<long, VariantSnapshot> Variants { get; set; } = [];

    public bool HasAnyAvailableVariant => Variants.Values.Any(v => v.Available);

    /// <summary>Null when no variant carries a price. Min over nullables yields null for an empty sequence.</summary>
    public decimal? LowestPrice => Variants.Values.Select(v => v.Price).Where(p => p.HasValue).Min();

    public string BuildUrl(string storeBaseUrl) => $"{storeBaseUrl.TrimEnd('/')}/products/{Handle}";
}

public sealed class VariantSnapshot
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public bool Available { get; set; }

    /// <summary>Shopify uses this literal for single-variant products; it is noise in an email.</summary>
    public bool IsDefaultTitle =>
        string.Equals(Title, "Default Title", StringComparison.OrdinalIgnoreCase);
}

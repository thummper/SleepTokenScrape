using System.Text.Json.Serialization;

namespace SleepTokenWatcher.State;

/// <summary>
/// The catalogue as it looked at one point in time. Persisted between runs and diffed against the next fetch.
/// </summary>
public sealed class CatalogSnapshot
{
    public DateTimeOffset CapturedAtUtc { get; set; }

    /// <summary>Keyed by the platform's product id.</summary>
    public Dictionary<string, ProductSnapshot> Products { get; set; } = [];
}

public sealed class ProductSnapshot
{
    /// <summary>Shopify ids are numeric, Squarespace ids are hex strings; both are stored as text.</summary>
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string? ProductType { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>Site-relative product page path. Null in state written before Squarespace support.</summary>
    public string? UrlPath { get; set; }

    /// <summary>Keyed by the platform's variant id.</summary>
    public Dictionary<string, VariantSnapshot> Variants { get; set; } = [];

    public bool HasAnyAvailableVariant => Variants.Values.Any(v => v.Available);

    /// <summary>Null when no variant carries a price. Min over nullables yields null for an empty sequence.</summary>
    public decimal? LowestPrice => Variants.Values.Select(v => v.Price).Where(p => p.HasValue).Min();

    public string BuildUrl(string storeBaseUrl) =>
        $"{storeBaseUrl.TrimEnd('/')}/{(UrlPath ?? $"products/{Handle}").TrimStart('/')}";
}

public sealed class VariantSnapshot
{
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public bool Available { get; set; }

    /// <summary>Shopify uses this literal for single-variant products; it is noise in an email.</summary>
    public bool IsDefaultTitle =>
        string.Equals(Title, "Default Title", StringComparison.OrdinalIgnoreCase);
}

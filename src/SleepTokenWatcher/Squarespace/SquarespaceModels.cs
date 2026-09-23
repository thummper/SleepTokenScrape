namespace SleepTokenWatcher.Squarespace;

/// <summary>Raw shape of a Squarespace products page served with ?format=json. Camel-case keys.</summary>
internal sealed class SquarespaceCollectionResponse
{
    public List<SquarespaceItem> Items { get; set; } = [];
    public SquarespacePagination? Pagination { get; set; }
}

internal sealed class SquarespacePagination
{
    public bool NextPage { get; set; }

    /// <summary>Site-relative, e.g. "/shop?offset=1712345678901". Lacks the format parameter.</summary>
    public string? NextPageUrl { get; set; }
}

internal sealed class SquarespaceItem
{
    /// <summary>24-character hex id.</summary>
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? UrlId { get; set; }

    /// <summary>Site-relative product page, e.g. "/shop/p/little-ghost".</summary>
    public string? FullUrl { get; set; }

    public string? AssetUrl { get; set; }

    /// <summary>Newer templates put variants here.</summary>
    public List<SquarespaceVariant>? Variants { get; set; }

    /// <summary>Older templates nest variants here instead; current ones fill both.</summary>
    public SquarespaceStructuredContent? StructuredContent { get; set; }
}

internal sealed class SquarespaceStructuredContent
{
    public List<SquarespaceVariant>? Variants { get; set; }
}

internal sealed class SquarespaceVariant
{
    /// <summary>A GUID string.</summary>
    public string Id { get; set; } = string.Empty;

    public string? Sku { get; set; }

    /// <summary>Option name to chosen value, e.g. {"Size": "M"}. Empty for single-variant products.</summary>
    public Dictionary<string, string>? Attributes { get; set; }

    /// <summary>Legacy minor-unit price (pence). Used only when the money objects are missing.</summary>
    public long? Price { get; set; }

    public long? SalePrice { get; set; }
    public SquarespaceMoney? PriceMoney { get; set; }
    public SquarespaceMoney? SalePriceMoney { get; set; }
    public bool OnSale { get; set; }

    /// <summary>True when stock is not tracked, which means always buyable.</summary>
    public bool Unlimited { get; set; }

    public int QtyInStock { get; set; }
}

internal sealed class SquarespaceMoney
{
    public string? Currency { get; set; }

    /// <summary>Invariant-culture decimal string, e.g. "25.00".</summary>
    public string? Value { get; set; }
}

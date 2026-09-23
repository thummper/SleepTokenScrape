using System.ComponentModel.DataAnnotations;

namespace SleepTokenWatcher.Configuration;

/// <summary>One storefront to watch. Configured as an entry in Watcher:Stores.</summary>
public sealed class StoreOptions
{
    /// <summary>Short, unique, file-name-safe id, e.g. "sleep-token". Names the state file and tags log lines.</summary>
    [Required]
    [RegularExpression("^[a-z0-9-]+$")]
    public string Key { get; set; } = string.Empty;

    /// <summary>Which storefront software serves the shop. Decides how the catalogue is fetched.</summary>
    public StorePlatform Platform { get; set; } = StorePlatform.Shopify;

    /// <summary>Shown in email headings and subjects.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Storefront root, e.g. https://storeuk.sleep-token.com.</summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Shopify: the collection handle. Squarespace: the shop page's URL slug (e.g. "shop").
    /// </summary>
    [Required]
    public string CollectionHandle { get; set; } = string.Empty;

    /// <summary>Prefixed to prices in emails.</summary>
    public string CurrencySymbol { get; set; } = "£";

    /// <summary>
    /// Treat an empty catalogue as a real state rather than a glitch. Needed for shops that sit empty between
    /// drops: without it the first cycle of a drop becomes the baseline and the drop is never emailed.
    /// </summary>
    public bool AllowEmptyCatalog { get; set; }

    /// <summary>File name inside Watcher:StateDirectory. Defaults to "{Key}.json".</summary>
    public string? StateFileName { get; set; }

    public string CollectionUrl => Platform switch
    {
        StorePlatform.Squarespace => $"{BaseUrl.TrimEnd('/')}/{CollectionHandle.Trim('/')}",
        _ => $"{BaseUrl.TrimEnd('/')}/collections/{CollectionHandle}",
    };

    public string ResolveStateFileName() =>
        string.IsNullOrWhiteSpace(StateFileName) ? $"{Key}.json" : StateFileName;
}

public enum StorePlatform
{
    /// <summary>Reads /collections/{handle}/products.json.</summary>
    Shopify,

    /// <summary>Reads /{page}?format=json.</summary>
    Squarespace,
}

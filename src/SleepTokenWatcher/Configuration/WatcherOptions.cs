using System.ComponentModel.DataAnnotations;

namespace SleepTokenWatcher.Configuration;

/// <summary>
/// Controls what is polled, how often, and where the seen-product state lives.
/// </summary>
public sealed class WatcherOptions
{
    public const string SectionName = "Watcher";

    /// <summary>Storefront root, e.g. https://storeuk.sleep-token.com.</summary>
    [Required]
    public string StoreBaseUrl { get; set; } = "https://storeuk.sleep-token.com";

    /// <summary>Shopify collection handle to watch.</summary>
    [Required]
    public string CollectionHandle { get; set; } = "all-products";

    /// <summary>How long to wait between checks.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>File holding the previous run's catalogue. Must be on a mounted volume to survive restarts.</summary>
    [Required]
    public string StateFilePath { get; set; } = "/data/state.json";

    /// <summary>Touched after every successful cycle so Docker's healthcheck can tell the loop is alive.</summary>
    public string HeartbeatFilePath { get; set; } = "/data/heartbeat";

    /// <summary>IANA time zone used for timestamps in emails. Falls back to UTC if unavailable.</summary>
    public string DisplayTimeZone { get; set; } = "Europe/London";

    /// <summary>Prefixed to prices in emails. The UK store sells in GBP.</summary>
    public string CurrencySymbol { get; set; } = "£";

    /// <summary>Sends a "watcher started" email on boot so you can confirm SMTP works without waiting for a drop.</summary>
    public bool SendStartupTestEmail { get; set; }

    /// <summary>Guards against an unbounded paging loop if the storefront misbehaves.</summary>
    public int MaxPages { get; set; } = 20;
}

using System.ComponentModel.DataAnnotations;

namespace SleepTokenWatcher.Configuration;

/// <summary>
/// Controls which stores are polled, how often, and where the seen-product state lives.
/// </summary>
public sealed class WatcherOptions
{
    public const string SectionName = "Watcher";

    /// <summary>Every storefront to watch. Each is fetched, diffed and emailed about independently.</summary>
    [MinLength(1)]
    public List<StoreOptions> Stores { get; set; } = [];

    /// <summary>How long to wait between checks.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Holds one state file per store. Must be a mounted volume to survive restarts.</summary>
    [Required]
    public string StateDirectory { get; set; } = "/data";

    /// <summary>Touched after every successful store check so Docker's healthcheck can tell the loop is alive.</summary>
    public string HeartbeatFilePath { get; set; } = "/data/heartbeat";

    /// <summary>IANA time zone used for timestamps in emails. Falls back to UTC if unavailable.</summary>
    public string DisplayTimeZone { get; set; } = "Europe/London";

    /// <summary>Sends a "watcher started" email when a store's baseline is first recorded, to prove SMTP works.</summary>
    public bool SendStartupTestEmail { get; set; }

    /// <summary>Guards against an unbounded paging loop if a storefront misbehaves.</summary>
    public int MaxPages { get; set; } = 20;

    /// <summary>Checks that data annotations cannot express: per-store fields and unique keys.</summary>
    public IEnumerable<string> Validate()
    {
        foreach (var store in Stores)
        {
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(store, new ValidationContext(store), results, validateAllProperties: true))
            {
                foreach (var result in results)
                {
                    yield return $"Store '{store.Key}': {result.ErrorMessage}";
                }
            }
        }

        foreach (var duplicate in Stores.GroupBy(s => s.Key).Where(g => g.Count() > 1))
        {
            yield return $"Store key '{duplicate.Key}' is used more than once; each store needs its own state file.";
        }

        foreach (var duplicate in Stores.GroupBy(s => s.ResolveStateFileName()).Where(g => g.Count() > 1))
        {
            yield return $"State file '{duplicate.Key}' is shared by more than one store.";
        }
    }
}

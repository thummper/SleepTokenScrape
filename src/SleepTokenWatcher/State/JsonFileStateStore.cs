using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SleepTokenWatcher.Configuration;

namespace SleepTokenWatcher.State;

/// <summary>
/// Persists each store's last-seen catalogue to its own JSON file on a mounted volume.
/// Writes go to a temp file first so a container kill mid-write cannot leave a truncated state file.
/// </summary>
public sealed class JsonFileStateStore(
    IOptions<WatcherOptions> options,
    ILogger<JsonFileStateStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly WatcherOptions _options = options.Value;

    /// <summary>Returns null when no usable prior state exists, which the worker treats as "seed a baseline".</summary>
    public async Task<CatalogSnapshot?> LoadAsync(StoreOptions store, CancellationToken cancellationToken)
    {
        var path = GetStatePath(store);

        if (!File.Exists(path))
        {
            logger.LogInformation("No state file at {Path}; this run will establish the baseline.", path);
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var snapshot = await JsonSerializer.DeserializeAsync<CatalogSnapshot>(stream, JsonOptions, cancellationToken);

            // An empty catalogue is only a real state for shops configured to sit empty between drops.
            if (snapshot is null || (snapshot.Products.Count == 0 && !store.AllowEmptyCatalog))
            {
                logger.LogWarning("State file at {Path} held no products; re-establishing the baseline.", path);
                return null;
            }

            return snapshot;
        }
        catch (JsonException ex)
        {
            // Corrupt state is recoverable: rebuild a baseline rather than crash-looping the container.
            logger.LogError(ex, "State file at {Path} is unreadable; re-establishing the baseline.", path);
            return null;
        }
    }

    public async Task SaveAsync(StoreOptions store, CatalogSnapshot snapshot, CancellationToken cancellationToken)
    {
        var path = GetStatePath(store);
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private string GetStatePath(StoreOptions store) =>
        Path.Combine(_options.StateDirectory, store.ResolveStateFileName());

    /// <summary>Best-effort liveness marker for the Docker healthcheck; never fails a cycle.</summary>
    public async Task TouchHeartbeatAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.HeartbeatFilePath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_options.HeartbeatFilePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(
                _options.HeartbeatFilePath,
                DateTimeOffset.UtcNow.ToString("O"),
                cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not update the heartbeat file.");
        }
    }
}

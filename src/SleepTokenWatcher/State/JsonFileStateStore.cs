using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SleepTokenWatcher.Configuration;

namespace SleepTokenWatcher.State;

/// <summary>
/// Persists the last-seen catalogue to a JSON file on a mounted volume.
/// Writes go to a temp file first so a container kill mid-write cannot leave a truncated state file.
/// </summary>
public sealed class JsonFileStateStore(
    IOptions<WatcherOptions> options,
    ILogger<JsonFileStateStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly WatcherOptions _options = options.Value;

    /// <summary>Returns null when no usable prior state exists, which the worker treats as "seed a baseline".</summary>
    public async Task<CatalogSnapshot?> LoadAsync(CancellationToken cancellationToken)
    {
        var path = _options.StateFilePath;

        if (!File.Exists(path))
        {
            logger.LogInformation("No state file at {Path}; this run will establish the baseline.", path);
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var snapshot = await JsonSerializer.DeserializeAsync<CatalogSnapshot>(stream, JsonOptions, cancellationToken);

            if (snapshot is null || snapshot.Products.Count == 0)
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

    public async Task SaveAsync(CatalogSnapshot snapshot, CancellationToken cancellationToken)
    {
        var path = _options.StateFilePath;
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

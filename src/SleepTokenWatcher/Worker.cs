using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SleepTokenWatcher.Catalog;
using SleepTokenWatcher.Configuration;
using SleepTokenWatcher.Detection;
using SleepTokenWatcher.Notifications;
using SleepTokenWatcher.State;

namespace SleepTokenWatcher;

/// <summary>
/// Polls every configured store on a fixed interval, diffs each against its last-seen catalogue,
/// and emails what changed. Stores are checked one after another so a slow or broken one cannot
/// stop the others.
/// </summary>
public sealed class Worker(
    IEnumerable<ICatalogClient> catalogClients,
    JsonFileStateStore stateStore,
    EmailNotifier notifier,
    IOptions<WatcherOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly WatcherOptions _options = options.Value;
    private readonly Dictionary<StorePlatform, ICatalogClient> _clients = catalogClients.ToDictionary(c => c.Platform);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var store in _options.Stores)
        {
            logger.LogInformation(
                "[{Store}] Watching {Url} ({Platform}).", store.Key, store.CollectionUrl, store.Platform);
        }

        logger.LogInformation("Checking {Count} stores every {Interval}.", _options.Stores.Count, _options.PollInterval);

        using var timer = new PeriodicTimer(_options.PollInterval);

        try
        {
            do
            {
                foreach (var store in _options.Stores)
                {
                    await RunCycleAsync(store, stoppingToken);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Shutdown requested; watcher stopping.");
        }
    }

    private async Task RunCycleAsync(StoreOptions store, CancellationToken cancellationToken)
    {
        try
        {
            var current = await _clients[store.Platform].FetchCatalogAsync(store, cancellationToken);

            if (current.Products.Count == 0 && !store.AllowEmptyCatalog)
            {
                // Treating an empty response as "everything vanished" would fire bogus alerts later,
                // so skip the cycle and keep the previous state intact.
                logger.LogWarning("[{Store}] The collection returned no products; skipping this cycle.", store.Key);
                return;
            }

            var previous = await stateStore.LoadAsync(store, cancellationToken);

            if (previous is null)
            {
                await stateStore.SaveAsync(store, current, cancellationToken);
                logger.LogInformation(
                    "[{Store}] Baseline established with {Count} products.", store.Key, current.Products.Count);

                if (_options.SendStartupTestEmail)
                {
                    await notifier.SendStartupTestAsync(store, current.Products.Count, cancellationToken);
                }

                await stateStore.TouchHeartbeatAsync(cancellationToken);
                return;
            }

            var changes = ChangeDetector.Compare(previous, current);

            if (changes.IsEmpty)
            {
                logger.LogInformation(
                    "[{Store}] No changes across {Count} products.", store.Key, current.Products.Count);
            }
            else
            {
                logger.LogInformation(
                    "[{Store}] Detected {New} new, {Restocked} restocked, {Repriced} repriced.",
                    store.Key, changes.NewProducts.Count, changes.BackInStock.Count, changes.PriceChanges.Count);

                // Send before persisting: if delivery fails, the next cycle re-detects the same changes
                // rather than silently swallowing them.
                await notifier.SendChangesAsync(store, changes, cancellationToken);
            }

            await stateStore.SaveAsync(store, current, cancellationToken);
            await stateStore.TouchHeartbeatAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never let one bad cycle kill a long-running container, or one store block the others.
            logger.LogError(ex, "[{Store}] Cycle failed; retrying at the next interval.", store.Key);
        }
    }
}

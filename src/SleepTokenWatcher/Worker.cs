using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SleepTokenWatcher.Configuration;
using SleepTokenWatcher.Detection;
using SleepTokenWatcher.Notifications;
using SleepTokenWatcher.Shopify;
using SleepTokenWatcher.State;

namespace SleepTokenWatcher;

/// <summary>
/// Polls the storefront on a fixed interval, diffs against the last-seen catalogue, and emails what changed.
/// </summary>
public sealed class Worker(
    ShopifyCatalogClient catalogClient,
    JsonFileStateStore stateStore,
    EmailNotifier notifier,
    IOptions<WatcherOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly WatcherOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Watching {Store}/collections/{Collection} every {Interval}.",
            _options.StoreBaseUrl.TrimEnd('/'), _options.CollectionHandle, _options.PollInterval);

        using var timer = new PeriodicTimer(_options.PollInterval);

        try
        {
            do
            {
                await RunCycleAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Shutdown requested; watcher stopping.");
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var current = await catalogClient.FetchCatalogAsync(cancellationToken);

            if (current.Products.Count == 0)
            {
                // Treating an empty response as "everything vanished" would fire bogus alerts later,
                // so skip the cycle and keep the previous state intact.
                logger.LogWarning("The collection returned no products; skipping this cycle.");
                return;
            }

            var previous = await stateStore.LoadAsync(cancellationToken);

            if (previous is null)
            {
                await stateStore.SaveAsync(current, cancellationToken);
                logger.LogInformation("Baseline established with {Count} products.", current.Products.Count);

                if (_options.SendStartupTestEmail)
                {
                    await notifier.SendStartupTestAsync(current.Products.Count, cancellationToken);
                }

                await stateStore.TouchHeartbeatAsync(cancellationToken);
                return;
            }

            var changes = ChangeDetector.Compare(previous, current);

            if (changes.IsEmpty)
            {
                logger.LogInformation("No changes across {Count} products.", current.Products.Count);
            }
            else
            {
                logger.LogInformation(
                    "Detected {New} new, {Restocked} restocked, {Repriced} repriced.",
                    changes.NewProducts.Count, changes.BackInStock.Count, changes.PriceChanges.Count);

                // Send before persisting: if delivery fails, the next cycle re-detects the same changes
                // rather than silently swallowing them.
                await notifier.SendChangesAsync(changes, cancellationToken);
            }

            await stateStore.SaveAsync(current, cancellationToken);
            await stateStore.TouchHeartbeatAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never let one bad cycle kill a long-running container.
            logger.LogError(ex, "Cycle failed; retrying at the next interval.");
        }
    }
}

using SleepTokenWatcher.Configuration;
using SleepTokenWatcher.State;

namespace SleepTokenWatcher.Catalog;

/// <summary>Fetches a store's watched collection from one storefront platform and maps it to a snapshot.</summary>
public interface ICatalogClient
{
    StorePlatform Platform { get; }

    Task<CatalogSnapshot> FetchCatalogAsync(StoreOptions store, CancellationToken cancellationToken);
}

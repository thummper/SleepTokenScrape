using SleepTokenWatcher.State;

namespace SleepTokenWatcher.Detection;

/// <summary>
/// Pure comparison of two catalogue snapshots. No I/O, so the alerting rules are directly testable.
/// </summary>
public static class ChangeDetector
{
    public static StoreChanges Compare(CatalogSnapshot previous, CatalogSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var newProducts = new List<ProductSnapshot>();
        var backInStock = new List<RestockedProduct>();
        var priceChanges = new List<PriceChange>();

        foreach (var (productId, product) in current.Products)
        {
            if (!previous.Products.TryGetValue(productId, out var previousProduct))
            {
                newProducts.Add(product);
                continue;
            }

            CollectRestocks(previousProduct, product, backInStock);
            CollectPriceChanges(previousProduct, product, priceChanges);
        }

        return new StoreChanges(
            [.. newProducts.OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase)],
            [.. backInStock.OrderBy(r => r.Product.Title, StringComparer.OrdinalIgnoreCase)],
            [.. priceChanges.OrderBy(c => c.Product.Title, StringComparer.OrdinalIgnoreCase)]);
    }

    /// <summary>
    /// Reports options that became buyable: either an unavailable option flipping to available,
    /// or a brand-new available option appearing on a product we already knew about.
    /// </summary>
    private static void CollectRestocks(
        ProductSnapshot previous,
        ProductSnapshot current,
        List<RestockedProduct> results)
    {
        var restocked = new List<RestockedVariant>();

        foreach (var (variantId, variant) in current.Variants)
        {
            if (!variant.Available)
            {
                continue;
            }

            if (previous.Variants.TryGetValue(variantId, out var previousVariant))
            {
                if (!previousVariant.Available)
                {
                    restocked.Add(new RestockedVariant(variant.Title, variant.Price, IsNewOption: false, variant.IsDefaultTitle));
                }
            }
            else
            {
                restocked.Add(new RestockedVariant(variant.Title, variant.Price, IsNewOption: true, variant.IsDefaultTitle));
            }
        }

        if (restocked.Count > 0)
        {
            results.Add(new RestockedProduct(current, [.. restocked.OrderBy(v => v.Title, StringComparer.OrdinalIgnoreCase)]));
        }
    }

    private static void CollectPriceChanges(
        ProductSnapshot previous,
        ProductSnapshot current,
        List<PriceChange> results)
    {
        foreach (var (variantId, variant) in current.Variants)
        {
            if (!previous.Variants.TryGetValue(variantId, out var previousVariant))
            {
                continue;
            }

            // A price appearing or disappearing is a data quirk, not a price change worth an email.
            if (variant.Price is not { } newPrice || previousVariant.Price is not { } oldPrice)
            {
                continue;
            }

            if (newPrice != oldPrice)
            {
                results.Add(new PriceChange(current, variant.Title, variant.IsDefaultTitle, oldPrice, newPrice));
            }
        }
    }
}

using SleepTokenWatcher.State;

namespace SleepTokenWatcher.Detection;

/// <summary>Everything worth emailing about from one comparison of two snapshots.</summary>
public sealed record StoreChanges(
    IReadOnlyList<ProductSnapshot> NewProducts,
    IReadOnlyList<RestockedProduct> BackInStock,
    IReadOnlyList<PriceChange> PriceChanges)
{
    public static StoreChanges None { get; } = new([], [], []);

    public bool IsEmpty => NewProducts.Count == 0 && BackInStock.Count == 0 && PriceChanges.Count == 0;
}

/// <summary>A product whose stock came back, with the specific options that flipped.</summary>
public sealed record RestockedProduct(ProductSnapshot Product, IReadOnlyList<RestockedVariant> Variants);

/// <param name="IsNewOption">True when the option did not exist last run (e.g. a size added), rather than a restock.</param>
public sealed record RestockedVariant(string Title, decimal? Price, bool IsNewOption, bool IsDefaultTitle);

public sealed record PriceChange(
    ProductSnapshot Product,
    string VariantTitle,
    bool IsDefaultTitle,
    decimal OldPrice,
    decimal NewPrice)
{
    public bool IsIncrease => NewPrice > OldPrice;
}

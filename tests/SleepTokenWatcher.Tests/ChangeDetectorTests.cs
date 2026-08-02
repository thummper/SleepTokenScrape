using SleepTokenWatcher.Detection;
using SleepTokenWatcher.State;
using Xunit;

namespace SleepTokenWatcher.Tests;

public class ChangeDetectorTests
{
    [Fact]
    public void Reports_a_product_that_did_not_exist_before()
    {
        var previous = Catalog(Product(1, "Bandana"));
        var current = Catalog(Product(1, "Bandana"), Product(2, "Hoodie"));

        var changes = ChangeDetector.Compare(previous, current);

        var added = Assert.Single(changes.NewProducts);
        Assert.Equal("Hoodie", added.Title);
        Assert.Empty(changes.BackInStock);
        Assert.Empty(changes.PriceChanges);
    }

    [Fact]
    public void Reports_nothing_when_the_catalogue_is_unchanged()
    {
        var previous = Catalog(Product(1, "Bandana"), Product(2, "Hoodie"));
        var current = Catalog(Product(1, "Bandana"), Product(2, "Hoodie"));

        Assert.True(ChangeDetector.Compare(previous, current).IsEmpty);
    }

    [Fact]
    public void Reports_a_variant_that_flips_from_sold_out_to_available()
    {
        var previous = Catalog(Product(1, "Hoodie", Variant(10, "M", 45m, available: false)));
        var current = Catalog(Product(1, "Hoodie", Variant(10, "M", 45m, available: true)));

        var changes = ChangeDetector.Compare(previous, current);

        var restock = Assert.Single(changes.BackInStock);
        var variant = Assert.Single(restock.Variants);
        Assert.Equal("M", variant.Title);
        Assert.False(variant.IsNewOption);
    }

    [Fact]
    public void Does_not_report_a_restock_when_the_variant_was_already_available()
    {
        var previous = Catalog(Product(1, "Hoodie", Variant(10, "M", 45m, available: true)));
        var current = Catalog(Product(1, "Hoodie", Variant(10, "M", 45m, available: true)));

        Assert.Empty(ChangeDetector.Compare(previous, current).BackInStock);
    }

    [Fact]
    public void Does_not_report_a_restock_when_a_variant_sells_out()
    {
        var previous = Catalog(Product(1, "Hoodie", Variant(10, "M", 45m, available: true)));
        var current = Catalog(Product(1, "Hoodie", Variant(10, "M", 45m, available: false)));

        Assert.True(ChangeDetector.Compare(previous, current).IsEmpty);
    }

    [Fact]
    public void Flags_a_newly_added_available_option_as_a_new_option()
    {
        var previous = Catalog(Product(1, "Hoodie", Variant(10, "M", 45m, available: true)));
        var current = Catalog(Product(1, "Hoodie",
            Variant(10, "M", 45m, available: true),
            Variant(11, "XL", 45m, available: true)));

        var restock = Assert.Single(ChangeDetector.Compare(previous, current).BackInStock);
        var variant = Assert.Single(restock.Variants);
        Assert.Equal("XL", variant.Title);
        Assert.True(variant.IsNewOption);
    }

    [Fact]
    public void Does_not_double_report_variants_of_a_brand_new_product()
    {
        var previous = Catalog(Product(1, "Bandana"));
        var current = Catalog(Product(1, "Bandana"), Product(2, "Hoodie", Variant(20, "M", 45m, available: true)));

        var changes = ChangeDetector.Compare(previous, current);

        Assert.Single(changes.NewProducts);
        Assert.Empty(changes.BackInStock);
    }

    [Fact]
    public void Reports_a_price_change_in_both_directions()
    {
        var previous = Catalog(Product(1, "Hoodie", Variant(10, "M", 45m, available: true)));
        var current = Catalog(Product(1, "Hoodie", Variant(10, "M", 39.99m, available: true)));

        var change = Assert.Single(ChangeDetector.Compare(previous, current).PriceChanges);
        Assert.Equal(45m, change.OldPrice);
        Assert.Equal(39.99m, change.NewPrice);
        Assert.False(change.IsIncrease);
    }

    [Fact]
    public void Ignores_a_price_that_only_appears_or_disappears()
    {
        var previous = Catalog(Product(1, "Hoodie", Variant(10, "M", null, available: true)));
        var current = Catalog(Product(1, "Hoodie", Variant(10, "M", 45m, available: true)));

        Assert.Empty(ChangeDetector.Compare(previous, current).PriceChanges);
    }

    [Fact]
    public void Ignores_products_that_disappear_from_the_collection()
    {
        var previous = Catalog(Product(1, "Bandana"), Product(2, "Hoodie"));
        var current = Catalog(Product(1, "Bandana"));

        Assert.True(ChangeDetector.Compare(previous, current).IsEmpty);
    }

    [Fact]
    public void Reports_every_kind_of_change_together()
    {
        var previous = Catalog(
            Product(1, "Hoodie", Variant(10, "M", 45m, available: false)),
            Product(2, "Tee", Variant(20, "L", 25m, available: true)));

        var current = Catalog(
            Product(1, "Hoodie", Variant(10, "M", 45m, available: true)),
            Product(2, "Tee", Variant(20, "L", 22m, available: true)),
            Product(3, "Vinyl", Variant(30, "Default Title", 30m, available: true)));

        var changes = ChangeDetector.Compare(previous, current);

        Assert.Single(changes.NewProducts);
        Assert.Single(changes.BackInStock);
        Assert.Single(changes.PriceChanges);
        Assert.False(changes.IsEmpty);
    }

    private static CatalogSnapshot Catalog(params ProductSnapshot[] products) =>
        new()
        {
            CapturedAtUtc = DateTimeOffset.UnixEpoch,
            Products = products.ToDictionary(p => p.Id),
        };

    private static ProductSnapshot Product(long id, string title, params VariantSnapshot[] variants)
    {
        if (variants.Length == 0)
        {
            variants = [Variant(id * 100, "Default Title", 20m, available: true)];
        }

        return new ProductSnapshot
        {
            Id = id,
            Title = title,
            Handle = title.ToLowerInvariant().Replace(' ', '-'),
            Variants = variants.ToDictionary(v => v.Id),
        };
    }

    private static VariantSnapshot Variant(long id, string title, decimal? price, bool available) =>
        new()
        {
            Id = id,
            Title = title,
            Price = price,
            Available = available,
        };
}

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SleepTokenWatcher.Catalog;
using SleepTokenWatcher.Configuration;
using SleepTokenWatcher.State;

namespace SleepTokenWatcher.Squarespace;

/// <summary>
/// Reads a Squarespace products page with ?format=json appended. Squarespace serves any page's underlying
/// collection as JSON this way, so there is no HTML parsing to break when the template changes.
/// </summary>
public sealed class SquarespaceCatalogClient(
    HttpClient httpClient,
    IOptions<WatcherOptions> options,
    ILogger<SquarespaceCatalogClient> logger) : ICatalogClient
{
    private const int MaxAttempts = 3;

    /// <summary>Squarespace gives single-variant products no option text; reuse Shopify's label so emails hide it.</summary>
    private const string DefaultVariantTitle = "Default Title";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WatcherOptions _options = options.Value;

    public StorePlatform Platform => StorePlatform.Squarespace;

    /// <summary>
    /// Fetches every visible product on the shop page, following the next-page links Squarespace returns.
    /// Hidden products are not in the feed, so a shop between drops legitimately returns nothing.
    /// </summary>
    public async Task<CatalogSnapshot> FetchCatalogAsync(StoreOptions store, CancellationToken cancellationToken)
    {
        var snapshot = new CatalogSnapshot { CapturedAtUtc = DateTimeOffset.UtcNow };
        var baseUrl = store.BaseUrl.TrimEnd('/');
        var path = $"/{store.CollectionHandle.Trim('/')}";

        for (var page = 1; page <= _options.MaxPages; page++)
        {
            var response = await GetWithRetryAsync(baseUrl + WithJsonFormat(path), cancellationToken);

            foreach (var item in response.Items)
            {
                var mapped = MapItem(item);
                snapshot.Products[mapped.Id] = mapped;
            }

            if (response.Pagination is not { NextPage: true, NextPageUrl: { Length: > 0 } next })
            {
                break;
            }

            if (page == _options.MaxPages)
            {
                logger.LogWarning(
                    "Stopped paging at the {MaxPages}-page cap; the shop may be larger than expected.",
                    _options.MaxPages);
                break;
            }

            path = next;
        }

        return snapshot;
    }

    internal static ProductSnapshot MapItem(SquarespaceItem item)
    {
        var snapshot = new ProductSnapshot
        {
            Id = item.Id,
            Title = item.Title,
            Handle = item.UrlId ?? string.Empty,
            UrlPath = item.FullUrl,
            ImageUrl = string.IsNullOrEmpty(item.AssetUrl) ? null : item.AssetUrl,
        };

        var variants = item.Variants is { Count: > 0 } ? item.Variants : item.StructuredContent?.Variants ?? [];

        foreach (var variant in variants)
        {
            snapshot.Variants[variant.Id] = new VariantSnapshot
            {
                Id = variant.Id,
                Title = BuildVariantTitle(variant),
                Sku = variant.Sku,
                Price = ResolvePrice(variant),
                Available = variant.Unlimited || variant.QtyInStock > 0,
            };
        }

        return snapshot;
    }

    private static string BuildVariantTitle(SquarespaceVariant variant) =>
        variant.Attributes is { Count: > 0 } attributes
            ? string.Join(" / ", attributes.Values)
            : DefaultVariantTitle;

    /// <summary>The price a buyer pays now: the sale price while on sale, otherwise the list price.</summary>
    private static decimal? ResolvePrice(SquarespaceVariant variant)
    {
        var money = variant.OnSale ? variant.SalePriceMoney : variant.PriceMoney;

        if (decimal.TryParse(money?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
        {
            return price;
        }

        var minorUnits = variant.OnSale ? variant.SalePrice : variant.Price;
        return minorUnits / 100m;
    }

    private static string WithJsonFormat(string path) =>
        path + (path.Contains('?') ? "&" : "?") + "format=json";

    private async Task<SquarespaceCollectionResponse> GetWithRetryAsync(
        string path,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var response = await httpClient.GetAsync(path, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var parsed = await JsonSerializer.DeserializeAsync<SquarespaceCollectionResponse>(
                    stream, JsonOptions, cancellationToken);

                return parsed ?? new SquarespaceCollectionResponse();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       && !cancellationToken.IsCancellationRequested
                                       && attempt < MaxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                logger.LogWarning(
                    ex,
                    "Fetch of {Path} failed (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}.",
                    path, attempt, MaxAttempts, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SleepTokenWatcher.Configuration;
using SleepTokenWatcher.State;

namespace SleepTokenWatcher.Shopify;

/// <summary>
/// Reads the storefront's public products.json feed. This is Shopify's own JSON view of the collection,
/// so there is no HTML parsing to break when the theme changes.
/// </summary>
public sealed class ShopifyCatalogClient(
    HttpClient httpClient,
    IOptions<WatcherOptions> options,
    ILogger<ShopifyCatalogClient> logger)
{
    private const int PageSize = 250;
    private const int MaxAttempts = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly WatcherOptions _options = options.Value;

    /// <summary>
    /// Fetches every published product in the watched collection, following pagination until an empty page.
    /// </summary>
    public async Task<CatalogSnapshot> FetchCatalogAsync(CancellationToken cancellationToken)
    {
        var snapshot = new CatalogSnapshot { CapturedAtUtc = DateTimeOffset.UtcNow };

        for (var page = 1; page <= _options.MaxPages; page++)
        {
            var path = $"/collections/{_options.CollectionHandle}/products.json?limit={PageSize}&page={page}";
            var response = await GetWithRetryAsync(path, cancellationToken);

            if (response.Products.Count == 0)
            {
                break;
            }

            foreach (var product in response.Products)
            {
                snapshot.Products[product.Id] = MapProduct(product);
            }

            // A short page means there is nothing after it.
            if (response.Products.Count < PageSize)
            {
                break;
            }

            if (page == _options.MaxPages)
            {
                logger.LogWarning(
                    "Stopped paging at the {MaxPages}-page cap; the collection may be larger than expected.",
                    _options.MaxPages);
            }
        }

        return snapshot;
    }

    private async Task<ShopifyProductsResponse> GetWithRetryAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var response = await httpClient.GetAsync(path, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var parsed = await JsonSerializer.DeserializeAsync<ShopifyProductsResponse>(
                    stream, JsonOptions, cancellationToken);

                return parsed ?? new ShopifyProductsResponse();
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

    private static ProductSnapshot MapProduct(ShopifyProduct product)
    {
        var snapshot = new ProductSnapshot
        {
            Id = product.Id,
            Title = product.Title,
            Handle = product.Handle,
            ProductType = product.ProductType,
            ImageUrl = product.Images.OrderBy(i => i.Position).Select(i => i.Src).FirstOrDefault(),
        };

        foreach (var variant in product.Variants)
        {
            snapshot.Variants[variant.Id] = new VariantSnapshot
            {
                Id = variant.Id,
                Title = variant.Title,
                Sku = variant.Sku,
                Price = ParsePrice(variant.Price),
                Available = variant.Available,
            };
        }

        return snapshot;
    }

    private static decimal? ParsePrice(string? raw) =>
        decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ? price : null;
}

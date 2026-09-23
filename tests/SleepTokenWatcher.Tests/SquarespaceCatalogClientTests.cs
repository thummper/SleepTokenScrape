using System.Text.Json;
using SleepTokenWatcher.Squarespace;
using SleepTokenWatcher.State;
using Xunit;

namespace SleepTokenWatcher.Tests;

public class SquarespaceCatalogClientTests
{
    // Trimmed from a live Squarespace 7.1 store's ?format=json response.
    private const string Feed =
        """
        {
          "items": [
            {
              "id": "5cf0515c56b12700013c35e6",
              "title": "Little Ghost",
              "urlId": "little-ghost",
              "fullUrl": "/shop/p/little-ghost",
              "assetUrl": "https://static1.squarespace.com/static/abc/def/",
              "variants": [
                {
                  "attributes": { "Colour": "White", "Size": "Small" },
                  "id": "35c22fad-657e-400f-bbab-d09439ff4442",
                  "sku": "SQ1712964",
                  "price": 4500,
                  "salePrice": 3500,
                  "priceMoney": { "currency": "GBP", "value": "45.00" },
                  "salePriceMoney": { "currency": "GBP", "value": "35.00" },
                  "onSale": true,
                  "unlimited": false,
                  "qtyInStock": 0
                }
              ]
            },
            {
              "id": "5cf0515c56b12700013c35e7",
              "title": "Bowler Hat",
              "urlId": "bowler-hat",
              "fullUrl": "/shop/p/bowler-hat",
              "variants": [],
              "structuredContent": {
                "variants": [
                  {
                    "attributes": {},
                    "id": "0f9c2a3e-1111-2222-3333-444455556666",
                    "price": 1200,
                    "onSale": false,
                    "unlimited": true,
                    "qtyInStock": 0
                  }
                ]
              }
            }
          ],
          "pagination": { "nextPage": true, "nextPageUrl": "/shop?offset=1712345678901" }
        }
        """;

    private static SquarespaceCollectionResponse Parse() =>
        JsonSerializer.Deserialize<SquarespaceCollectionResponse>(Feed, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void Maps_a_product_with_named_options_and_a_sale_price()
    {
        var product = SquarespaceCatalogClient.MapItem(Parse().Items[0]);

        Assert.Equal("5cf0515c56b12700013c35e6", product.Id);
        Assert.Equal("https://www.yorkghostmerchants.com/shop/p/little-ghost",
            product.BuildUrl("https://www.yorkghostmerchants.com/"));

        var variant = Assert.Single(product.Variants.Values);
        Assert.Equal("White / Small", variant.Title);
        Assert.False(variant.IsDefaultTitle);
        Assert.Equal(35.00m, variant.Price);
        Assert.False(variant.Available);
    }

    [Fact]
    public void Falls_back_to_structured_content_variants_and_minor_unit_prices()
    {
        var product = SquarespaceCatalogClient.MapItem(Parse().Items[1]);

        var variant = Assert.Single(product.Variants.Values);
        Assert.True(variant.IsDefaultTitle);
        Assert.Equal(12.00m, variant.Price);
        Assert.True(variant.Available, "Unlimited stock means always buyable.");
    }

    [Fact]
    public void Reads_the_next_page_link()
    {
        var pagination = Parse().Pagination;

        Assert.NotNull(pagination);
        Assert.True(pagination.NextPage);
        Assert.Equal("/shop?offset=1712345678901", pagination.NextPageUrl);
    }

    [Fact]
    public void Reads_state_files_written_when_ids_were_numbers()
    {
        const string legacyState =
            """
            {
              "CapturedAtUtc": "2026-08-01T12:00:00+00:00",
              "Products": {
                "123": {
                  "Id": 123,
                  "Title": "Hoodie",
                  "Handle": "hoodie",
                  "Variants": { "456": { "Id": 456, "Title": "M", "Price": 45.0, "Available": true } }
                }
              }
            }
            """;

        var snapshot = JsonSerializer.Deserialize<CatalogSnapshot>(legacyState)!;

        var product = snapshot.Products["123"];
        Assert.Equal("123", product.Id);
        Assert.Equal("456", product.Variants["456"].Id);
        Assert.Equal("https://storeuk.sleep-token.com/products/hoodie",
            product.BuildUrl("https://storeuk.sleep-token.com"));
    }
}

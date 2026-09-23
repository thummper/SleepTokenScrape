using System.Globalization;
using System.Net;
using System.Text;
using SleepTokenWatcher.Configuration;
using SleepTokenWatcher.Detection;
using SleepTokenWatcher.State;

namespace SleepTokenWatcher.Notifications;

/// <summary>Turns one store's <see cref="StoreChanges"/> into a subject line and an HTML/plain-text body pair.</summary>
public sealed class ChangeEmailBuilder(StoreOptions store, string displayTimeZone)
{

    public string BuildSubject(StoreChanges changes)
    {
        var parts = new List<string>(3);

        if (changes.NewProducts.Count > 0)
        {
            parts.Add($"{changes.NewProducts.Count} new {Plural(changes.NewProducts.Count, "product")}");
        }

        if (changes.BackInStock.Count > 0)
        {
            parts.Add($"{changes.BackInStock.Count} back in stock");
        }

        if (changes.PriceChanges.Count > 0)
        {
            parts.Add($"{changes.PriceChanges.Count} price {Plural(changes.PriceChanges.Count, "change")}");
        }

        return $"{store.Name}: {string.Join(", ", parts)}";
    }

    public string BuildHtmlBody(StoreChanges changes)
    {
        var html = new StringBuilder();

        html.Append(
            """
            <div style="font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Helvetica,Arial,sans-serif;
                        max-width:640px;margin:0 auto;padding:24px;color:#1a1a1a;">
            """);

        html.Append(
            $"""
             <h1 style="font-size:20px;margin:0 0 4px;">{WebUtility.HtmlEncode(store.Name)}</h1>
             <p style="margin:0 0 24px;color:#666;font-size:13px;">Checked {WebUtility.HtmlEncode(FormatNow())}</p>
             """);

        if (changes.NewProducts.Count > 0)
        {
            AppendSectionHeading(html, "New products", "#0a7f3f");

            foreach (var product in changes.NewProducts)
            {
                AppendProductCard(html, product, DescribeNewProduct(product));
            }
        }

        if (changes.BackInStock.Count > 0)
        {
            AppendSectionHeading(html, "Back in stock", "#0b62c4");

            foreach (var restock in changes.BackInStock)
            {
                AppendProductCard(html, restock.Product, DescribeRestock(restock));
            }
        }

        if (changes.PriceChanges.Count > 0)
        {
            AppendSectionHeading(html, "Price changes", "#a8560a");

            foreach (var group in changes.PriceChanges.GroupBy(c => c.Product.Id))
            {
                var product = group.First().Product;
                var lines = group.Select(DescribePriceChange);
                AppendProductCard(html, product, string.Join("<br>", lines));
            }
        }

        html.Append(
            $"""
             <p style="margin:32px 0 0;font-size:12px;color:#999;border-top:1px solid #eee;padding-top:12px;">
               <a href="{WebUtility.HtmlEncode(CollectionUrl)}" style="color:#999;">View the full collection</a>
             </p>
             </div>
             """);

        return html.ToString();
    }

    /// <summary>Plain-text alternative for clients that will not render HTML.</summary>
    public string BuildTextBody(StoreChanges changes)
    {
        var text = new StringBuilder();
        text.AppendLine(store.Name);
        text.AppendLine($"Checked {FormatNow()}");
        text.AppendLine();

        if (changes.NewProducts.Count > 0)
        {
            text.AppendLine("NEW PRODUCTS");

            foreach (var product in changes.NewProducts)
            {
                text.AppendLine($"- {product.Title}{FormatPriceSuffix(product)}");
                text.AppendLine($"  {product.BuildUrl(store.BaseUrl)}");
            }

            text.AppendLine();
        }

        if (changes.BackInStock.Count > 0)
        {
            text.AppendLine("BACK IN STOCK");

            foreach (var restock in changes.BackInStock)
            {
                var options = restock.Variants.Where(v => !v.IsDefaultTitle).Select(v => v.Title).ToList();
                var suffix = options.Count > 0 ? $" ({string.Join(", ", options)})" : string.Empty;
                text.AppendLine($"- {restock.Product.Title}{suffix}");
                text.AppendLine($"  {restock.Product.BuildUrl(store.BaseUrl)}");
            }

            text.AppendLine();
        }

        if (changes.PriceChanges.Count > 0)
        {
            text.AppendLine("PRICE CHANGES");

            foreach (var change in changes.PriceChanges)
            {
                var option = change.IsDefaultTitle ? string.Empty : $" [{change.VariantTitle}]";
                text.AppendLine(
                    $"- {change.Product.Title}{option}: {Money(change.OldPrice)} -> {Money(change.NewPrice)}");
                text.AppendLine($"  {change.Product.BuildUrl(store.BaseUrl)}");
            }

            text.AppendLine();
        }

        text.AppendLine(CollectionUrl);
        return text.ToString();
    }

    private string CollectionUrl => store.CollectionUrl;

    private static void AppendSectionHeading(StringBuilder html, string title, string colour) =>
        html.Append(
            $"""
             <h2 style="font-size:13px;text-transform:uppercase;letter-spacing:0.08em;
                        color:{colour};margin:28px 0 12px;">{WebUtility.HtmlEncode(title)}</h2>
             """);

    private void AppendProductCard(StringBuilder html, ProductSnapshot product, string detailHtml)
    {
        var url = WebUtility.HtmlEncode(product.BuildUrl(store.BaseUrl));
        var title = WebUtility.HtmlEncode(product.Title);

        html.Append(
            """
            <div style="display:flex;gap:14px;align-items:flex-start;padding:12px 0;border-bottom:1px solid #f0f0f0;">
            """);

        if (!string.IsNullOrWhiteSpace(product.ImageUrl))
        {
            var image = WebUtility.HtmlEncode(product.ImageUrl);
            html.Append(
                $"""
                 <a href="{url}"><img src="{image}" alt="" width="72" height="72"
                    style="width:72px;height:72px;object-fit:cover;border-radius:6px;background:#f6f6f6;"></a>
                 """);
        }

        html.Append(
            $"""
             <div>
               <a href="{url}" style="font-size:15px;font-weight:600;color:#111;text-decoration:none;">{title}</a>
               <div style="font-size:13px;color:#555;margin-top:4px;">{detailHtml}</div>
             </div>
             </div>
             """);
    }

    private string DescribeNewProduct(ProductSnapshot product)
    {
        var parts = new List<string>(2);
        var price = product.LowestPrice;

        if (price.HasValue)
        {
            var prefix = product.Variants.Count > 1 ? "from " : string.Empty;
            parts.Add(WebUtility.HtmlEncode($"{prefix}{Money(price.Value)}"));
        }

        parts.Add(product.HasAnyAvailableVariant
            ? "<span style=\"color:#0a7f3f;\">In stock</span>"
            : "<span style=\"color:#b00020;\">Sold out</span>");

        return string.Join(" &middot; ", parts);
    }

    private string DescribeRestock(RestockedProduct restock)
    {
        var named = restock.Variants.Where(v => !v.IsDefaultTitle).ToList();

        if (named.Count == 0)
        {
            var price = restock.Variants[0].Price;
            return price.HasValue ? WebUtility.HtmlEncode(Money(price.Value)) : "Available again";
        }

        var labels = named.Select(v =>
            WebUtility.HtmlEncode(v.IsNewOption ? $"{v.Title} (new option)" : v.Title));

        return "Available again: " + string.Join(", ", labels);
    }

    private string DescribePriceChange(PriceChange change)
    {
        var arrow = change.IsIncrease ? "&uarr;" : "&darr;";
        var colour = change.IsIncrease ? "#b00020" : "#0a7f3f";
        var option = change.IsDefaultTitle
            ? string.Empty
            : WebUtility.HtmlEncode($"{change.VariantTitle}: ");

        return $"""
                {option}<span style="text-decoration:line-through;color:#999;">{WebUtility.HtmlEncode(Money(change.OldPrice))}</span>
                <span style="color:{colour};font-weight:600;"> {arrow} {WebUtility.HtmlEncode(Money(change.NewPrice))}</span>
                """;
    }

    private string FormatPriceSuffix(ProductSnapshot product) =>
        product.LowestPrice is { } price ? $" — {Money(price)}" : string.Empty;

    private string Money(decimal amount) =>
        $"{store.CurrencySymbol}{amount.ToString("0.00", CultureInfo.InvariantCulture)}";

    private string FormatNow()
    {
        var now = DateTimeOffset.UtcNow;

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(displayTimeZone);
            return TimeZoneInfo.ConvertTime(now, zone).ToString("dddd d MMMM, HH:mm", CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return now.ToString("dddd d MMMM, HH:mm 'UTC'", CultureInfo.InvariantCulture);
        }
    }

    private static string Plural(int count, string word) => count == 1 ? word : word + "s";
}

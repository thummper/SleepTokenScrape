using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SleepTokenWatcher;
using SleepTokenWatcher.Catalog;
using SleepTokenWatcher.Configuration;
using SleepTokenWatcher.Notifications;
using SleepTokenWatcher.Shopify;
using SleepTokenWatcher.Squarespace;
using SleepTokenWatcher.State;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<WatcherOptions>()
    .Bind(builder.Configuration.GetSection(WatcherOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<WatcherOptions>, WatcherOptionsValidator>();

builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<ShopifyCatalogClient>(ConfigureStoreClient);
builder.Services.AddHttpClient<SquarespaceCatalogClient>(ConfigureStoreClient);

builder.Services.AddTransient<ICatalogClient>(provider => provider.GetRequiredService<ShopifyCatalogClient>());
builder.Services.AddTransient<ICatalogClient>(provider => provider.GetRequiredService<SquarespaceCatalogClient>());

builder.Services.AddSingleton<JsonFileStateStore>();
builder.Services.AddSingleton<EmailNotifier>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();

static void ConfigureStoreClient(IServiceProvider provider, HttpClient client)
{
    // No BaseAddress: one client serves every store on its platform, so requests use absolute URLs.
    client.Timeout = TimeSpan.FromSeconds(30);

    // Both platforms serve their JSON feeds to ordinary clients; a real UA avoids being treated as an unknown bot.
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "SleepTokenWatcher/1.0 (+https://github.com/; personal stock notifier)");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
}

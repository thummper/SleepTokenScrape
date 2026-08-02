using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SleepTokenWatcher;
using SleepTokenWatcher.Configuration;
using SleepTokenWatcher.Notifications;
using SleepTokenWatcher.Shopify;
using SleepTokenWatcher.State;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<WatcherOptions>()
    .Bind(builder.Configuration.GetSection(WatcherOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<ShopifyCatalogClient>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<WatcherOptions>>().Value;

    client.BaseAddress = new Uri(options.StoreBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);

    // Shopify serves products.json to ordinary clients; a real UA avoids being treated as an unknown bot.
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "SleepTokenWatcher/1.0 (+https://github.com/; personal stock notifier)");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddSingleton<JsonFileStateStore>();
builder.Services.AddSingleton<ChangeEmailBuilder>();
builder.Services.AddSingleton<EmailNotifier>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();

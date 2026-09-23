using Microsoft.Extensions.Options;

namespace SleepTokenWatcher.Configuration;

/// <summary>Fails startup with every store problem listed, instead of starting a watcher that cannot work.</summary>
public sealed class WatcherOptionsValidator : IValidateOptions<WatcherOptions>
{
    public ValidateOptionsResult Validate(string? name, WatcherOptions options)
    {
        var failures = options.Validate().ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

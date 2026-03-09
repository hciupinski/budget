using Microsoft.Extensions.Options;

namespace Budget.Api.Modules.Auth;

public sealed class OwnerAccountStartupLogger(
    IOptions<OwnerAccountOptions> options,
    ILogger<OwnerAccountStartupLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var owner = options.Value;
        var legacyEnabled = owner.AllowLegacyPlaintextPassword && !string.IsNullOrWhiteSpace(owner.Password);
        var hasHash = !string.IsNullOrWhiteSpace(owner.PasswordHash);

        if (legacyEnabled)
        {
            logger.LogWarning(
                "OwnerAccount legacy plaintext password mode is enabled. This should be temporary. Set OwnerAccount:PasswordHash and disable OwnerAccount:AllowLegacyPlaintextPassword.");
        }

        if (!legacyEnabled && !hasHash)
        {
            logger.LogWarning(
                "OwnerAccount has no usable credentials configured. Set OwnerAccount:PasswordHash (recommended) or enable temporary legacy mode.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

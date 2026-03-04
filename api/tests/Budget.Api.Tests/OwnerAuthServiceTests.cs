using Budget.Api.Modules.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Budget.Api.Tests;

public sealed class OwnerAuthServiceTests
{
    [Fact]
    public void ValidateCredentials_AcceptsValidPasswordHash()
    {
        var hasher = new PasswordHasher<object>();
        var hash = hasher.HashPassword(new object(), "Correct!Password123");

        var service = CreateService(new OwnerAccountOptions
        {
            Email = "owner@example.com",
            PasswordHash = hash,
            AllowLegacyPlaintextPassword = false
        });

        var result = service.ValidateCredentials("owner@example.com", "Correct!Password123");

        Assert.True(result.IsValid);
        Assert.False(result.UsedLegacyPlaintextPassword);
    }

    [Fact]
    public void ValidateCredentials_RejectsInvalidPasswordHash()
    {
        var hasher = new PasswordHasher<object>();
        var hash = hasher.HashPassword(new object(), "Correct!Password123");

        var service = CreateService(new OwnerAccountOptions
        {
            Email = "owner@example.com",
            PasswordHash = hash,
            AllowLegacyPlaintextPassword = false
        });

        var result = service.ValidateCredentials("owner@example.com", "WrongPassword");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateCredentials_UsesLegacyPasswordWhenEnabled()
    {
        var service = CreateService(new OwnerAccountOptions
        {
            Email = "owner@example.com",
            Password = "legacy-password-123",
            AllowLegacyPlaintextPassword = true
        });

        var result = service.ValidateCredentials("owner@example.com", "legacy-password-123");

        Assert.True(result.IsValid);
        Assert.True(result.UsedLegacyPlaintextPassword);
    }

    [Fact]
    public void ValidateCredentials_RejectsLegacyPasswordWhenDisabled()
    {
        var service = CreateService(new OwnerAccountOptions
        {
            Email = "owner@example.com",
            Password = "legacy-password-123",
            AllowLegacyPlaintextPassword = false
        });

        var result = service.ValidateCredentials("owner@example.com", "legacy-password-123");

        Assert.False(result.IsValid);
    }

    private static OwnerAuthService CreateService(OwnerAccountOptions options)
    {
        return new OwnerAuthService(
            Options.Create(options),
            new PasswordHasher<object>(),
            NullLogger<OwnerAuthService>.Instance);
    }
}

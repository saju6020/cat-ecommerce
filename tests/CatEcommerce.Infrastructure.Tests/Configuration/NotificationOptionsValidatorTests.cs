using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Configuration;

/// <summary>
/// HO-NTF-001 §15, U-B16. <c>WebBaseUrl</c> moved to the shared <c>PublicSiteOptions.WebBaseUrl</c>
/// (integrator instruction, 2026-09-27, post HO-CAT-001-gates merge) -- validated by
/// <c>PublicSiteStartupGuardHostedService</c> instead, so every case here now only exercises
/// <c>ApiBaseUrl</c>, the setting that stayed.
/// </summary>
public sealed class NotificationOptionsValidatorTests
{
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public void Production_WithHttpApiBaseUrl_Fails()
    {
        var validator = new NotificationOptionsValidator(new FakeEnvironment(Environments.Production));
        var options = new NotificationOptions { ApiBaseUrl = "http://api.example.com" };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Production_WithDevelopmentSender_Fails()
    {
        var validator = new NotificationOptionsValidator(new FakeEnvironment(Environments.Production));
        var options = new NotificationOptions
        {
            ApiBaseUrl = "https://api.example.com",
            Channels = new NotificationChannelsOptions { Sms = new NotificationChannelSenderOptions { Sender = "Development" }, Email = new NotificationChannelSenderOptions { Sender = "Unconfigured" } }
        };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Production_WithHttpsAndUnconfigured_Succeeds()
    {
        var validator = new NotificationOptionsValidator(new FakeEnvironment(Environments.Production));
        var options = new NotificationOptions { ApiBaseUrl = "https://api.example.com" };

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Development_WithHttpApiBaseUrlAndDevelopmentSender_Succeeds()
    {
        var validator = new NotificationOptionsValidator(new FakeEnvironment(Environments.Development));
        var options = new NotificationOptions
        {
            ApiBaseUrl = "http://localhost:5000",
            Channels = new NotificationChannelsOptions { Sms = new NotificationChannelSenderOptions { Sender = "Development" }, Email = new NotificationChannelSenderOptions { Sender = "Development" } }
        };

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    /// <summary>HO-NTF-001 SECURITY/ARCHITECTURE gate fix (2026-09-27, LOW): the committed
    /// `.invalid`-shaped placeholders fail safe (dead links resolve nowhere) but silently -- a
    /// deployment that forgets to set the real base URL must not start cleanly outside
    /// Development/Testing.</summary>
    [Theory]
    [InlineData("https://api.invalid")]
    [InlineData("https://api.example")]
    [InlineData("https://api.test")]
    [InlineData("https://localhost")]
    public void Production_WithPlaceholderHost_Fails(string placeholderUrl)
    {
        var validator = new NotificationOptionsValidator(new FakeEnvironment(Environments.Production));
        var options = new NotificationOptions { ApiBaseUrl = placeholderUrl };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Testing_WithPlaceholderHost_Succeeds()
    {
        var validator = new NotificationOptionsValidator(new FakeEnvironment("Testing"));
        var options = new NotificationOptions { ApiBaseUrl = "https://api.example.test" };

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    /// <summary>HO-NTF-001 CODE_REVIEW gate fix (2026-09-27, LOW): the lease must outlive the send
    /// timeout, or a slow send can outlast its own claim.</summary>
    [Fact]
    public void LeaseSecondsNotGreaterThanSendTimeoutSeconds_Fails()
    {
        var validator = new NotificationOptionsValidator(new FakeEnvironment(Environments.Production));
        var options = new NotificationOptions
        {
            ApiBaseUrl = "https://api.example.com",
            Worker = new NotificationWorkerOptions { LeaseSeconds = 10, SendTimeoutSeconds = 10 }
        };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
    }
}

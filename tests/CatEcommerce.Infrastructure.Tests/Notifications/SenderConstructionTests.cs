using CatEcommerce.Infrastructure.Notifications.Senders;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Notifications;

/// <summary>HO-NTF-001 §15, U-B15.</summary>
public sealed class SenderConstructionTests
{
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public void DevelopmentChannelSender_ConstructedInProduction_Throws()
    {
        var environment = new FakeEnvironment(Environments.Production);
        Assert.Throws<InvalidOperationException>(() =>
            new DevelopmentChannelSender(NotificationChannel.Sms, environment, NullLogger<DevelopmentChannelSender>.Instance));
    }

    [Fact]
    public void DevelopmentChannelSender_ConstructedInDevelopment_Succeeds()
    {
        var environment = new FakeEnvironment(Environments.Development);
        var sender = new DevelopmentChannelSender(NotificationChannel.Sms, environment, NullLogger<DevelopmentChannelSender>.Instance);
        Assert.Equal("Development", sender.ProviderName);
    }

    [Fact]
    public void CapturingChannelSender_ConstructedInProduction_Throws()
    {
        var environment = new FakeEnvironment(Environments.Production);
        Assert.Throws<InvalidOperationException>(() => new CapturingChannelSender(NotificationChannel.Sms, environment));
    }

    [Fact]
    public void CapturingChannelSender_ConstructedInTesting_Succeeds()
    {
        var environment = new FakeEnvironment("Testing");
        var sender = new CapturingChannelSender(NotificationChannel.Sms, environment);
        Assert.Equal("Capturing", sender.ProviderName);
    }

    [Fact]
    public async Task UnconfiguredChannelSender_AlwaysPermanentFailure()
    {
        var sender = new UnconfiguredChannelSender(NotificationChannel.Sms);
        var outcome = await sender.SendAsync(new CatEcommerce.Domain.DomainServices.OutgoingMessage(
            Guid.NewGuid(), "+8801700000000", null, "body", null, new Dictionary<string, string>()));

        Assert.Equal(SendOutcomeKind.PermanentFailure, outcome.Kind);
        Assert.Contains("No SMS provider", outcome.ProviderResponse);
    }
}

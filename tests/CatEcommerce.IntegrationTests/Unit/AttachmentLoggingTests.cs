using System.Text;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Notifications.Senders;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>HO-RPT-001 section 11.7 item 4 -- U-G9: the development sender logs an attachment's file name and size, never its content (HO-NTF-001 section 8.9 rule 7).</summary>
public sealed class AttachmentLoggingTests
{
    private sealed class CapturingLogger : ILogger<DevelopmentChannelSender>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }

    private sealed class Development : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public async Task U_G9_TheDevelopmentSender_LogsTheFileNameAndSize_AndNeverTheContent()
    {
        var logger = new CapturingLogger();
        var sender = new DevelopmentChannelSender(NotificationChannel.Email, new Development(), logger);
        var secret = "customer-name,total\r\nSECRET-CONTENT-MARKER,1500.00\r\n";
        var bytes = Encoding.UTF8.GetBytes(secret);

        var outcome = await sender.SendAsync(
            new OutgoingMessage(Guid.NewGuid(), "owner@example.test", "Report", "Body", null, new Dictionary<string, string>(),
                [new OutgoingAttachment("purrcare-revenue.csv", "text/csv", bytes)]),
            CancellationToken.None);

        Assert.Equal(SendOutcomeKind.Accepted, outcome.Kind);
        var log = string.Join("\n", logger.Lines);
        Assert.Contains("purrcare-revenue.csv", log);
        Assert.Contains($"{bytes.Length} bytes", log);
        Assert.DoesNotContain("SECRET-CONTENT-MARKER", log);
        Assert.DoesNotContain(Convert.ToBase64String(bytes), log);
    }

    [Fact]
    public void AMessageWithoutAttachments_HasAnEmptyList_NotNull()
    {
        var message = new OutgoingMessage(Guid.NewGuid(), "+8801700000000", null, "body", null, new Dictionary<string, string>());
        Assert.Empty(message.AttachmentList);
    }
}

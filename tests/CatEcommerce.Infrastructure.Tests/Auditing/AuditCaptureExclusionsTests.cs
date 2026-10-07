using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Infrastructure.Auditing;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Auditing;

/// <summary>HO-NTF-001 §15, U-X2 / ADR-066.</summary>
public sealed class AuditCaptureExclusionsTests
{
    [Fact]
    public void ContainsExactlyTheAuditRootAndTheTwoNotifyRoots()
    {
        Assert.Equal(3, AuditCaptureExclusions.Types.Count);
        Assert.Contains(typeof(AuditEntryAggregate), AuditCaptureExclusions.Types);
        Assert.Contains(typeof(NotificationRequestAggregate), AuditCaptureExclusions.Types);
        Assert.Contains(typeof(NotificationAggregate), AuditCaptureExclusions.Types);
    }

    [Fact]
    public void NotificationTemplateAggregate_IsNotExcluded_TemplateEditsAreAuditedNormally()
    {
        Assert.DoesNotContain(typeof(NotificationTemplateAggregate), AuditCaptureExclusions.Types);
    }
}

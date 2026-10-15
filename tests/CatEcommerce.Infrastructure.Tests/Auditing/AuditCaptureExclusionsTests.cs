using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Infrastructure.Auditing;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Auditing;

/// <summary>HO-NTF-001 §15, U-X2 / ADR-066.</summary>
public sealed class AuditCaptureExclusionsTests
{
    [Fact]
    public void ContainsExactlyTheAuditRootTheTwoNotifyRootsTheConsultationDraftAndTheInventoryEntryRoots()
    {
        Assert.Equal(8, AuditCaptureExclusions.Types.Count);
        Assert.Contains(typeof(CatEcommerce.Domain.Aggregates.Operations.WebVitalBatchAggregate), AuditCaptureExclusions.Types);
        // HO-XCT-001 section 5.3 / ADR-303 appended RetentionRunAggregate (its summary BusinessEvent row replaces the DataChange rows).
        Assert.Contains(typeof(CatEcommerce.Domain.Aggregates.Operations.RetentionRunAggregate), AuditCaptureExclusions.Types);
        // HO-EMR-001 §12.1 / U-X2 appended ConsultationDraftAggregate.
        Assert.Contains(typeof(ConsultationDraftAggregate), AuditCaptureExclusions.Types);
        Assert.Contains(typeof(AuditEntryAggregate), AuditCaptureExclusions.Types);
        Assert.Contains(typeof(NotificationRequestAggregate), AuditCaptureExclusions.Types);
        Assert.Contains(typeof(NotificationAggregate), AuditCaptureExclusions.Types);
        // HO-IMP-001 ADR-098: each ledger row already records who, what, when and why, so it is not audited a second time.
        Assert.Contains(typeof(StockLedgerEntryAggregate), AuditCaptureExclusions.Types);
        Assert.Contains(typeof(StockReservationEntryAggregate), AuditCaptureExclusions.Types);
    }

    [Fact]
    public void InventoryBatchesAndOtherRoots_AreAuditedNormally()
    {
        Assert.DoesNotContain(typeof(BatchAggregate), AuditCaptureExclusions.Types);
        Assert.DoesNotContain(typeof(GoodsReceiptAggregate), AuditCaptureExclusions.Types);
    }

    [Fact]
    public void NotificationTemplateAggregate_IsNotExcluded_TemplateEditsAreAuditedNormally()
    {
        Assert.DoesNotContain(typeof(NotificationTemplateAggregate), AuditCaptureExclusions.Types);
    }

    [Fact]
    public void VisitAggregate_IsNeverExcluded_TheRecordItselfIsAudited() // HO-EMR-001 U-X2
    {
        Assert.DoesNotContain(typeof(VisitAggregate), AuditCaptureExclusions.Types);
        Assert.DoesNotContain(typeof(VisitAddendumAggregate), AuditCaptureExclusions.Types);
        Assert.DoesNotContain(typeof(DiagnosticOrderAggregate), AuditCaptureExclusions.Types);
        Assert.DoesNotContain(typeof(ConsentRecordAggregate), AuditCaptureExclusions.Types);
    }
}

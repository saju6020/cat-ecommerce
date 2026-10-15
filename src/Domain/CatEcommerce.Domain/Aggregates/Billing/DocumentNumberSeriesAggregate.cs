using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 §8.7 (ADR-179) -- one row per <c>(branch, financial year, document kind)</c> holding the
/// next sequence number. <see cref="Take"/> returns the number and advances it; the series row and the
/// document commit in the <b>same</b> <c>SaveChangesAsync</c>, guarded by <c>xmin</c>, so the number is
/// taken and committed with the document or not at all -- no gap, no reuse. Raises no event (a counter).
/// </summary>
public sealed class DocumentNumberSeriesAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public string FinancialYear { get; private set; } = default!;
    public BillingDocumentKind DocumentKind { get; private set; } = default!;
    public long NextSequence { get; private set; }

    private DocumentNumberSeriesAggregate() { }

    public DocumentNumberSeriesAggregate(Guid id) : base(id) { }

    public void Open(Guid branchId, string financialYear, BillingDocumentKind documentKind, Platform.Infrastructure.Common.Security.UserContext userContext)
    {
        BranchId = branchId;
        FinancialYear = financialYear;
        DocumentKind = documentKind;
        NextSequence = 1;

        SetDefaultValue(userContext);
        UpdateVersion();
    }

    public long Take()
    {
        var number = NextSequence;
        NextSequence++;
        UpdateVersion();
        return number;
    }
}

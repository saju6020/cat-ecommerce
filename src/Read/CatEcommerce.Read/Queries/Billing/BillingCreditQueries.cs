using CatEcommerce.Infrastructure.Billing.Documents;
using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Billing;

/// <summary>HO-BIL-001 §12.4 -- a credit note detail (staff, branch-scoped).</summary>
public sealed class GetCreditNoteDetailQuery : Query<CreditNoteDetailViewModel?>
{
    public required Guid CreditNoteId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>The data of a credit-note PDF (staff, branch-scoped).</summary>
public sealed class GetCreditNoteDocumentQuery : Query<CreditNoteDocument?>
{
    public required Guid CreditNoteId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>Refunds at the active branch, newest first.</summary>
public sealed class GetRefundsQuery : Query<PagedResult<RefundListItemViewModel>>
{
    public required Guid BranchId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Billing;

/// <summary>HO-BIL-001 section 11.1 C26 (US-BIL-09, ADR-190) -- records that an accounting export was produced: range, branch, row counts and the
/// SHA-256 of the file. A command of its own because the export itself is a read and a query never writes.</summary>
public sealed class RecordAccountingExportRunCommand : Command
{
    public required Guid RunId { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public Guid? BranchId { get; init; }
    public required int InvoiceCount { get; init; }
    public required int InvoiceLineCount { get; init; }
    public required int PaymentCount { get; init; }
    public required int AllocationCount { get; init; }
    public required int CreditNoteCount { get; init; }
    public required int RefundCount { get; init; }
    public required string Sha256 { get; init; }
}

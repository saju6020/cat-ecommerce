using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Billing;

/// <summary>HO-BIL-001 section 11.1 C20 (US-BIL-12, US-BIL-06 AC3) -- record (or replace) the counted cash of a day; the branch is the active branch.</summary>
public sealed class RecordCashCountCommand : Command
{
    public required Guid BranchId { get; init; }
    public required DateOnly BusinessDate { get; init; }
    public required decimal CountedCash { get; init; }
    public string? Explanation { get; init; }
}

/// <summary>C21 -- close the day: freeze the expected totals, compute the variance. Terminal.</summary>
public sealed class CloseCashUpCommand : Command
{
    public required Guid BranchId { get; init; }
    public required DateOnly BusinessDate { get; init; }
    public required int ExpectedVersion { get; init; }
}

using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Boarding;

// HO-BRD-001 section 11.1 C20-C23 (Phase G).

public sealed class ReferStayCatToClinicCommand : Command
{
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }

    /// <summary>A <c>TriagePriority</c> name.</summary>
    public required string Urgency { get; init; }
}

public sealed class RecordReferralReturnCommand : Command
{
    public required Guid StayId { get; init; }
    public required Guid ReferralId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class RequestVetChargeApprovalCommand : Command
{
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
    public Guid? ReferralId { get; init; }
    public required decimal Amount { get; init; }
    public required string Description { get; init; }
}

/// <summary>The owner answers on the portal (own stay only).</summary>
public sealed class RespondToVetChargeApprovalCommand : Command
{
    public required Guid StayId { get; init; }
    public required Guid ApprovalId { get; init; }
    public required Guid CustomerId { get; init; }
    public required bool Approve { get; init; }
    public string? Note { get; init; }
}

/// <summary>Staff record an answer the owner gave by phone; the note is required and the answer is attributed.</summary>
public sealed class RecordVetChargeApprovalByStaffCommand : Command
{
    public required Guid StayId { get; init; }
    public required Guid ApprovalId { get; init; }
    public required Guid BranchId { get; init; }
    public required bool Approve { get; init; }
    public required string Note { get; init; }
}

public sealed class ExtendBoardingStayCommand : Command
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required DateOnly NewDepartureOn { get; init; }
}

/// <summary>Moves the stay to another unit and, when given, extends it in the same command (one aggregate).</summary>
public sealed class RelocateBoardingStayCommand : Command
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required Guid TargetUnitId { get; init; }
    public required DateOnly EffectiveFromLocalDate { get; init; }
    public required string Reason { get; init; }
    public DateOnly? NewDepartureOn { get; init; }
}

public sealed record BelongingReturnItem(Guid BelongingId, bool Returned, string? Note);

public sealed record CheckOutCatInput(Guid CatId, decimal? WeightKg, IReadOnlyList<BelongingReturnItem> Belongings, SignatureInput? Signature);

public sealed class CheckOutBoardingStayCommand : Command
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required IReadOnlyList<CheckOutCatInput> Cats { get; init; }
}

public sealed class ClearStayAttentionCommand : Command
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Note { get; init; }
}

/// <summary>Worker only: issues the invoice for the stay's uninvoiced charges up to a cycle boundary (US-BRD-20). Idempotent by key.</summary>
public sealed class InvoiceBoardingCycleCommand : Command
{
    public required Guid StayId { get; init; }
    public required int Cycle { get; init; }
    public required DateOnly ServiceDateTo { get; init; }
}

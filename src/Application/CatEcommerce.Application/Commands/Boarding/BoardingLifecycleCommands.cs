using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Boarding;

// HO-BRD-001 section 11.1 C9-C13 (Phase D). The worker sends Confirm / Lapse / Expire with no caller scope: IsWorker is set only by BoardingWorkCycle and is never
// bound from an HTTP request.

/// <summary>C9 -- staff (after recording the deposit in Billing) or the worker. Reads Billing's accepted payments; never records money.</summary>
public sealed class ConfirmBoardingStayCommand : Command, IStayScoped
{
    public required Guid StayId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? BranchId { get; init; }

    /// <summary>The worker skips silently when the deposit is not ready; staff are told how much is outstanding.</summary>
    public bool IsWorker { get; init; }
}

/// <summary>C10 -- worker only.</summary>
public sealed class LapseBoardingReservationCommand : Command
{
    public required Guid StayId { get; init; }
}

public sealed class ExpireBoardingEnquiryCommand : Command
{
    public required Guid StayId { get; init; }
}

/// <summary>C11 -- a customer cancels their own stay, staff cancel with a note (BR-12).</summary>
public sealed class CancelBoardingStayCommand : Command, IStayScoped
{
    public required Guid StayId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? BranchId { get; init; }
    public string? Note { get; init; }
}

/// <summary>C13 -- staff, after the grace period.</summary>
public sealed class RecordBoardingNoShowCommand : Command, IStayScoped
{
    public required Guid StayId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? BranchId { get; init; }
}

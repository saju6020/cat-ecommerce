using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Prescription;

// HO-PHM-001 §11.1 C12-C16 (US-PHM-06, US-PHM-07; ADR-142, ADR-148). Ids are minted in the controller; every command is dispatched with ValidateCommand = true (F3).

/// <summary>C12 -- the owner requests a repeat in the portal. The prescription's cat must be the caller's CURRENT cat (anything else is "not found"); the request is refused with
/// the date it could be repeated until, and what to do, once the vet's validity has ended (US-PHM-06 AC2).</summary>
public sealed class RequestRepeatCommand : Command
{
    public required Guid PrescriptionId { get; init; }
    public required Guid RepeatRequestId { get; init; }
    public required Guid PreferredBranchId { get; init; }
    public string? Note { get; init; }
}

/// <summary>C13 -- the owner withdraws their own open request. Anything else (not theirs, not open, unknown) succeeds without writing: withdrawing is idempotent.</summary>
public sealed class WithdrawRepeatRequestCommand : Command
{
    public required Guid PrescriptionId { get; init; }
    public required Guid RepeatRequestId { get; init; }
}

/// <summary>C14 -- a vet declines an open request with a reason (a clinical judgement); the owner is told, with the reason as plain text.</summary>
public sealed class DeclineRepeatRequestCommand : Command
{
    public required Guid PrescriptionId { get; init; }
    public required Guid RepeatRequestId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>C15 -- a vet renews a prescription (BR-6, ADR-148): a NEW prescription with the same medicine and the vet's possibly edited fields, the FULL safety assessment run again on
/// today's weight, allergies and medications, and the old one linked (and cancelled if still running). With <see cref="VisitId"/> it is written from that open visit; without one it
/// is a remote renewal against the original's visit, flagged <c>IsRemoteRenewal</c> (OQ-3). One save.</summary>
public sealed class RenewPrescriptionCommand : PrescribingFieldsCommandBase
{
    public required Guid NewPrescriptionId { get; init; }
    public required Guid FromPrescriptionId { get; init; }
    public Guid? VisitId { get; init; }
}

/// <summary>C16 -- the worker records that a prescription's validity has passed with supplies left. Idempotent: not expirable -> success, nothing written. Nothing waits for
/// it: every read and command already treats a passed <c>ValidUntil</c> as expired (ADR-142).</summary>
public sealed class ExpirePrescriptionCommand : Command
{
    public required Guid PrescriptionId { get; init; }
}

/// <summary>The answer to a successful repeat request.</summary>
public sealed record RequestedRepeat(Guid RepeatRequestId);

/// <summary>The extra a refused repeat request carries so the portal can offer the next step (US-PHM-06 AC2): where to book.</summary>
public sealed record RepeatRefusal(string BookLink);

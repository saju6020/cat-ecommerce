using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

// HO-BRD-001 Phase G -- referral, vet approvals, extension and check-out inputs (section 11.3). Facts the aggregate cannot know are loaded by the handler.

public sealed record ReferDto(Guid CatId, string Reason, TriagePriority Urgency, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record RequestVetApprovalDto(Guid CatId, Guid? ReferralId, decimal Amount, string Description, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

/// <summary><paramref name="Via"/> is Portal for the owner and Staff for a phone answer (note required).</summary>
public sealed record DecideVetApprovalDto(Guid ApprovalId, bool Approved, string? Note, AcknowledgementChannel Via, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record RecordVetCommitmentDto(Guid ChargeId, Guid CatId, decimal Amount, DateTimeOffset Now);

/// <summary>
/// The stay's new departure with the window and the re-quote computed from the stay's own snapshots (<see cref="BoardingQuoteCalculator"/>). The gate is re-evaluated as of the new
/// departure: a cat whose vaccination would lapse before it refuses the extension.
/// </summary>
public sealed record ExtendDto(
    DateOnly NewDepartureOn, OccupancyRange Window, BoardingQuote Quote, IReadOnlyDictionary<Guid, GateEvaluation> Gates, int MaxStayNights, DateOnly Today,
    DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

/// <summary>
/// Per cat the checklist; <paramref name="EarlyQuote"/> is the re-quote for the nights actually charged when the check-out is before the booked departure (otherwise <c>null</c>) and
/// <paramref name="ChargedDepartureOn"/> the departure those nights end on; <paramref name="TruncatedWindow"/> frees the unit at the check-out instant plus the turnaround.
/// </summary>
public sealed record CheckOutDto(
    IReadOnlyList<CheckOutCatDto> Cats, DateOnly Today, DateTimeOffset Now, OccupancyRange TruncatedWindow, BoardingQuote? EarlyQuote, DateOnly ChargedDepartureOn,
    UserContext UserContext, Guid CorrelationId);

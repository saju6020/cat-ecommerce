using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

// HO-BRD-001 section 11.3 -- the immutable inputs of the stay aggregate. Facts the aggregate cannot know (the branch time zone, the settings in force, the
// unit the allocator chose, the gate outcome of the cats) are loaded by the handler and passed in; the RULES stay in the aggregate (rule 20).

/// <summary>The branch facts the date rules need.</summary>
public sealed record StayDateContext(string TimeZoneId, TimeRange CheckInWindow, int MaxStayNights, int BookingHorizonDays, bool IsOnline);

public sealed record StayCatInput(Guid CatId, string CatName);

public sealed record EnquiryDto(
    Guid BranchId, Guid CustomerId, BoardingChannel Channel, DateOnly ArrivalOn, DateOnly DepartureOn, BoardingUnitType UnitType,
    IReadOnlyList<StayCatInput> Cats, IReadOnlyDictionary<Guid, GateEvaluation> Gates, OccupancyRange Window, StayDateContext Dates,
    DateTimeOffset Now, string CurrencyCode, UserContext UserContext, Guid CorrelationId);

public sealed record ChangeEnquiryDto(
    DateOnly ArrivalOn, DateOnly DepartureOn, BoardingUnitType UnitType, IReadOnlyList<StayCatInput> Cats,
    IReadOnlyDictionary<Guid, GateEvaluation> Gates, OccupancyRange Window, StayDateContext Dates,
    DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

/// <summary><paramref name="ByStaff"/> and <paramref name="VetAuthorityReason"/>: once a stay is reserved, staff changing a cat vet authorisation or spending limit must give a reason (audited with the before and after values).</summary>
public sealed record SetCareProfileDto(Guid CatId, CareProfile Profile, UserContext UserContext, Guid CorrelationId, bool ByStaff = false, string? VetAuthorityReason = null);

public sealed record OverrideGateDto(Guid CatId, string Reason, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

/// <summary>The unit the allocator chose (deterministic) and what the aggregate needs to know of it.</summary>
public sealed record AllocatedUnit(Guid UnitId, BoardingUnitType Type, int CapacityCats);

public sealed record ReserveDto(
    AllocatedUnit Unit, RateCardSnapshot Card, PricingSnapshot Pricing, BoardingQuote Quote, DepositRequirement Deposit,
    IReadOnlyDictionary<Guid, GateEvaluation> Gates, OccupancyRange Window, DateTimeOffset Now, DateOnly Today, int HoldHours, string CurrencyCode,
    IReadOnlyDictionary<Guid, string> CatNames, UserContext UserContext, Guid CorrelationId);

public sealed record ConfirmStayDto(
    decimal AcceptedDeposit, IReadOnlyDictionary<Guid, GateEvaluation> Gates, DateOnly Today, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record LapseStayDto(DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record CancelStayDto(bool ByCustomer, string? Note, DateTimeOffset Now, int WindowHours, UserContext UserContext, Guid CorrelationId);

public sealed record NoShowDto(DateTimeOffset Now, int GraceHours, UserContext UserContext, Guid CorrelationId);

public sealed record BelongingInput(BelongingKind Kind, string Description, IReadOnlyList<Guid> FileIds);

public sealed record CheckInCatDto(
    Guid CatId, bool IdentityConfirmed, bool VaccinationProofVerified, decimal? WeightKg, string? ArrivalHealthObservation,
    IReadOnlyList<BelongingInput> Belongings, bool NoBelongings, SignatureCapture? Signature);

public sealed record CheckInDto(
    IReadOnlyList<CheckInCatDto> Cats, IReadOnlyDictionary<Guid, GateEvaluation> Gates, DateOnly Today, DateTimeOffset Now, bool RequireBelongingsPhotos,
    UserContext UserContext, Guid CorrelationId);

public sealed record BelongingReturnInput(Guid BelongingId, bool Returned, string? Note);

public sealed record CheckOutCatDto(Guid CatId, decimal? WeightKg, IReadOnlyList<BelongingReturnInput> Belongings, SignatureCapture? Signature);

public sealed record FlagInfectiousDto(Guid CatId, string Note, AllocatedUnit? IsolationUnit, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record ClearInfectiousDto(Guid CatId, string Note, UserContext UserContext, Guid CorrelationId);

/// <summary>Moves the stay to another unit from <paramref name="FromInstant"/>: the current allocation is truncated there and a new one begins (section 8.15).</summary>
public sealed record RelocateDto(AllocatedUnit Unit, DateTimeOffset FromInstant, string Reason, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

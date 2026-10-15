namespace CatEcommerce.Shared.Common.Enums;

// HO-BRD-001 §10.1 -- the stay's own enumerations (Appendix C's states exactly, ADR-218 as renumbered ADR-351). One file; each type persisted by Value (ADR-022).

/// <summary>Appendix C. A lapse is <c>Cancelled</c> with <see cref="BoardingCancellationReason.ReservationLapsed"/>, not a state of its own.</summary>
public sealed class BoardingStayStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BoardingStayStatus Enquiry = new(1, nameof(Enquiry));
    public static readonly BoardingStayStatus Reserved = new(2, nameof(Reserved));
    public static readonly BoardingStayStatus Confirmed = new(3, nameof(Confirmed));
    public static readonly BoardingStayStatus CheckedIn = new(4, nameof(CheckedIn));
    public static readonly BoardingStayStatus CheckedOut = new(5, nameof(CheckedOut));
    public static readonly BoardingStayStatus Cancelled = new(6, nameof(Cancelled));
    public static readonly BoardingStayStatus NoShow = new(7, nameof(NoShow));
}

public sealed class BoardingCancellationReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BoardingCancellationReason ByCustomer = new(1, nameof(ByCustomer));
    public static readonly BoardingCancellationReason ByStaff = new(2, nameof(ByStaff));
    public static readonly BoardingCancellationReason ReservationLapsed = new(3, nameof(ReservationLapsed));
    public static readonly BoardingCancellationReason EnquiryExpired = new(4, nameof(EnquiryExpired));
}

public sealed class BoardingChannel(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BoardingChannel Online = new(1, nameof(Online));
    public static readonly BoardingChannel Reception = new(2, nameof(Reception));
}

public sealed class DepositState(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DepositState NotRequired = new(1, nameof(NotRequired));
    public static readonly DepositState Awaiting = new(2, nameof(Awaiting));
    public static readonly DepositState PendingApproval = new(3, nameof(PendingApproval));
    public static readonly DepositState Received = new(4, nameof(Received));
}

public sealed class StayRateBasis(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StayRateBasis Nightly = new(1, nameof(Nightly));
    public static readonly StayRateBasis Weekly = new(2, nameof(Weekly));
    public static readonly StayRateBasis Monthly = new(3, nameof(Monthly));
}

public sealed class RateUnitKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RateUnitKind Month = new(1, nameof(Month));
    public static readonly RateUnitKind Week = new(2, nameof(Week));
    public static readonly RateUnitKind Night = new(3, nameof(Night));
    public static readonly RateUnitKind AdditionalCat = new(4, nameof(AdditionalCat));
}

public sealed class VaccinationGateStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly VaccinationGateStatus Passed = new(1, nameof(Passed));
    public static readonly VaccinationGateStatus Blocked = new(2, nameof(Blocked));
    public static readonly VaccinationGateStatus Overridden = new(3, nameof(Overridden));
}

/// <summary>Why a stay is flagged for staff. Set by cat events or a maintenance overlap; cleared by staff. It never cancels anything (§20.7).</summary>
public sealed class StayAttentionReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StayAttentionReason CatDeceased = new(1, nameof(CatDeceased));
    public static readonly StayAttentionReason CatOwnerChanged = new(2, nameof(CatOwnerChanged));
    public static readonly StayAttentionReason MaintenanceOverlap = new(3, nameof(MaintenanceOverlap));
    public static readonly StayAttentionReason IsolationUnavailable = new(4, nameof(IsolationUnavailable));

    /// <summary>A reserved, paid stay whose cat no longer passes the vaccination gate: it cannot be confirmed; staff are told and the lapse may proceed.</summary>
    public static readonly StayAttentionReason VaccinationGateBlocked = new(5, nameof(VaccinationGateBlocked));

    /// <summary>The cat of the stay before this one on the same unit has not left yet (a late check-out, or still in the unit): staff should move this booking.</summary>
    public static readonly StayAttentionReason UnitOverstayed = new(6, nameof(UnitOverstayed));
}

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-BRD-001 sections 8.1 and 10.3 -- stay constants and the bounds of every owned collection (rule 3: anything that grows with days
/// times events is its own root, not a child of the stay).</summary>
public static class StayRules
{
    public const int MaxCatsPerStay = 6;

    /// <summary>SECURITY 2026-10-15 -- sensible defaults (no setting exists): one customer holds at most this many live waitlist entries and future daycare visits.</summary>
    public const int MaxLiveWaitlistEntriesPerCustomer = 10;
    public const int MaxFutureDaycareVisitsPerCustomer = 30;
    public const int NightsPerCycle = 30;
    public const int NightsPerWeekUnit = 7;
    public const int NightsPerMonthUnit = 30;

    public const int MaxAllocations = 10;
    public const int MaxRateUnits = 40;
    public const int MaxChargeLines = 60;
    public const int MaxReferrals = 20;
    public const int MaxApprovals = 20;
    public const int MaxVetCommitments = 100;
    public const int MaxStatusHistory = 20;
    public const int MaxDepositPayments = 10;

    public const int MaxCancellationNoteLength = 500;
    public const int MaxReasonLength = 500;
    public const int MaxUnitCodeLength = 20;
    public const int MaxUnitNameLength = 100;
    public const int MaxUnitBlockLength = 50;
    public const int MaxMaintenanceReasonLength = 200;
    public const int MaxUnitCapacity = 6;
    public const int MaxIsolationCapacity = 2;
}

/// <summary>HO-BRD-001 section 8.7 -- the bounds of a per-cat care profile (FR-BRD-6).</summary>
public static class CareProfileRules
{
    public const int MaxFeedings = 6;
    public const int MaxMedications = 10;
    public const int MaxMedicationTimes = 6;
    public const int MaxBelongings = 30;
    public const int MaxPortionLength = 100;
    public const int MaxFoodDescriptionLength = 200;
    public const int MaxMedicationNameLength = 100;
    public const int MaxDoseLength = 100;
    public const int MaxRouteLength = 50;
    public const int MaxInstructionsLength = 500;
    public const int MaxLitterNoteLength = 200;
    public const int MaxContactNameLength = 200;
    public const int MaxContactPhoneLength = 30;
    public const int MaxContactRelationshipLength = 50;
    public const int MaxNotesLength = 1000;
    public const int MaxBelongingDescriptionLength = 200;
    public const int MaxBelongingPhotos = 3;
}

/// <summary>HO-BRD-001 section 8.2 -- the care routine a branch has until its administrator sets one.</summary>
public static class BoardingRoutineDefaults
{
    public const int MaxTimesPerKind = 8;
    public const int MaxTimes = 24;

    public static readonly IReadOnlyList<(string Kind, TimeOnly Time)> Times =
    [
        ("LitterChange", new TimeOnly(9, 0)),
        ("LitterChange", new TimeOnly(17, 0)),
        ("Enrichment", new TimeOnly(15, 0)),
        ("ObservationCheck", new TimeOnly(10, 0)),
        ("ObservationCheck", new TimeOnly(20, 0)),
    ];
}

/// <summary>HO-BRD-001 sections 8.9, 8.13 and 8.17 -- care task, incident and media text bounds.</summary>
public static class IncidentRules
{
    public const int MaxDescriptionLength = 2000;
    public const int MaxImmediateActionLength = 2000;
    public const int MaxPhoneOutcomeLength = 500;
    public const int MaxResolutionLength = 1000;
    public const int MaxTaskTitleLength = 200;
    public const int MaxTaskNoteLength = 500;
    public const int MaxSkipReasonLength = 200;
    public const int MaxObservationNotesLength = 1000;
    public const int MaxIsolationPurposeLength = 200;
    public const int MaxCaptionLength = 300;
    public const int MaxVetApprovalDescriptionLength = 200;
    public const int MaxReferralReasonLength = 500;
    public const int MaxApprovalNoteLength = 500;
}

/// <summary>HO-BRD-001 section 8.17 (ADR-236 as renumbered ADR-369) -- a signature is bounded vector data or a typed confirmation, never a file.</summary>
public static class BoardingSignatureRules
{
    public const int MaxSerialisedBytes = 32 * 1024;
    public const int MaxCoordinate = 10_000;
    public const int MaxSignerNameLength = 200;
}

/// <summary>HO-BRD-001 section 8.17 -- stay media are HO-FIL-001 stored files referenced by id.</summary>
public static class MediaRules
{
    public const int MaxDiaryRangeDays = 31;
}

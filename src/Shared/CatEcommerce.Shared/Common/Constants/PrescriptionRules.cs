using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-PHM-001 §10.3 -- limits and shape rules for a prescription, shared by the aggregate, the validators and the reads.</summary>
public static class PrescriptionRules
{
    public const int MaxRepeats = 12;
    public const int MinDurationDays = 1;
    public const int MaxDurationDays = 365;
    public const int MaxInstructionsLength = 1000;
    public const int MaxReasonLength = 500;
    public const int MaxNoteLength = 500;
    public const int MaxWarnings = 30;
    public const int MaxWarningSubjectLength = 200;
    public const int MaxWarningDetailLength = 500;

    /// <summary>"RX-2026-000123" -- <c>{0}</c> the issue date (year), <c>{1}</c> the sequence value.</summary>
    public const string NumberFormat = "RX-{0:yyyy}-{1:000000}";

    /// <summary>A prescription may start no earlier than today minus this many days (§8.3).</summary>
    public const int MaxBackdatedStartDays = 1;

    public static string FormatNumber(DateTimeOffset issuedOn, long sequence) => string.Format(System.Globalization.CultureInfo.InvariantCulture, NumberFormat, issuedOn, sequence);
}

/// <summary>HO-PHM-001 §8.7 -- dispensing limits.</summary>
public static class DispensingRules
{
    public const int MaxLinesPerDispensing = 5;
    public const int MaxReasonLength = 500;
    public const int MinFefoOverrideReasonLength = 5;
}

/// <summary>HO-PHM-001 §8.8 -- repeat limits.</summary>
public static class RepeatRules
{
    public const int MaxRequestsPerPrescription = 30;
    public const int MaxNoteLength = 500;
    public const int MaxDeclineReasonLength = 500;
}

/// <summary>HO-PHM-001 §8.9 (ADR-144 … ADR-146) -- the controlled-drug register's rules.</summary>
public static class ControlledDrugRules
{
    /// <summary>OQ-1: any staff member holding one of these roles may witness (and must not be the person acting).</summary>
    public static readonly IReadOnlyList<string> WitnessRoles = [StaffRoles.Vet, StaffRoles.Nurse];

    public const int SealSettleSeconds = 30;
    public const int SealBatchSize = 500;
    public const int UnsealedAlertMinutes = 10;
    public const int MaxScheduleLength = 30;
    public const int MaxDoseTextLength = 200;
    public const int MaxNotesLength = 500;
    public const int MaxReasonLength = 500;
    public const int ExportMaxRows = 50_000;
    public const int ExportMaxRangeDays = 366;
    public const int EnlistedOccurredWindowHours = 24;
    public const string ZeroHash = "0000000000000000000000000000000000000000000000000000000000000000";

    /// <summary>The kinds that need a second person (BR-7): everything that moves or voids controlled stock out of a person's hands.</summary>
    public static bool RequiresWitness(ControlledDrugEntryKind kind) =>
        kind == ControlledDrugEntryKind.Dispensed || kind == ControlledDrugEntryKind.Administered || kind == ControlledDrugEntryKind.Wastage
        || kind == ControlledDrugEntryKind.Returned || kind == ControlledDrugEntryKind.Correction;
}

/// <summary>HO-PHM-001 §8.2 -- formulary limits.</summary>
public static class FormularyRules
{
    public const int MaxInteractions = 50;
    public const int MaxLinkedSkus = 10;
    public const int MaxActiveIngredients = 5;
    public const int MaxDrugClasses = 10;
    public const int MaxTermLength = 100;
    public const int MaxNameLength = 200;
    public const int MaxCodeLength = 30;
    public const int MaxContraindicationsLength = 2000;
    public const int MaxInteractionNoteLength = 500;
    public const int MaxReasonLength = 500;
}

/// <summary>HO-PHM-001 §8.10 -- diet authorisation limits.</summary>
public static class DietRules
{
    public const int MaxNotesLength = 500;
    public const int MaxReasonLength = 500;

    /// <summary>An authorisation that lapsed within this many days may still be extended (re-activated).</summary>
    public const int ReactivationWindowDays = 30;
}

/// <summary>HO-PHM-001 §8.7 / OQ-2 -- the standard warnings printed on every medicine label.</summary>
public static class LabelRules
{
    public const string StandardWarnings = "For animal treatment only. Keep out of reach of children.";
    public const string ControlledNotice = "Controlled drug";
}

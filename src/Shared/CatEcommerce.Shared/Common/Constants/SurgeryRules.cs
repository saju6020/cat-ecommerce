namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-SRG-001 §10.3.</summary>
public static class SurgeryRules
{
    /// <summary>Roles that may assist in theatre.</summary>
    public static readonly IReadOnlyList<string> AssistantRoleIds = [StaffRoles.Nurse, StaffRoles.Vet];

    public const int MaxAnaesthesiaEntries = 2000;
    public const int OverrideReasonMin = 10;
    public const int OverrideReasonMax = 1000;
    public const int CorrectionReasonMin = 10;
    public const int CorrectionReasonMax = 500;
    public const int NoteMax = 500;
    public const int WaiverReasonMax = 500;
    public const int CancellationReasonMax = 500;
    public const int EligibilityNoteMax = 500;
    public const int PostOpInstructionsMax = 4000;
    public const int FastingInstructionsMax = 2000;
    public const int ReportTextMax = 4000;
    public const int AddendumMax = 4000;
    public const int MaxFollowUps = 5;
    public const int MaxStatusHistory = 20;
    public const int MaxRecoveryHistory = 50;
    public const int MaxAddenda = 50;
    public const int MaxCampaignPrices = 20;
    public const int CampaignCodeMax = 30;
    public const int CampaignNameMax = 200;
    public const int CampaignDescriptionMax = 2000;

    /// <summary>An entry may be dated this far into the future (clock skew between the theatre screen and the server).</summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(2);

    /// <summary>An entry may pre-date the admission by this much (premedication is given just before admission is clicked).</summary>
    public static readonly TimeSpan BeforeAdmissionTolerance = TimeSpan.FromHours(1);

    /// <summary>The hour (branch-local) a follow-up reminder is sent on the day it falls due minus the lead days.</summary>
    public const int FollowUpReminderLocalHour = 10;

    /// <summary>A controlled-drug register quantity carries at most two decimals (the register's own rule).</summary>
    public const int ControlledQuantityDecimals = 2;
}

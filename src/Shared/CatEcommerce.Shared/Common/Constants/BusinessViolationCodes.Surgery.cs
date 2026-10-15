namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-SRG-001 section 10.4 -- Surgery violation codes. See ADR-341. Codes another area already declares (SlotNotOnGrid, CatNotBookable,
/// CatAlreadyBooked, ServiceNotBookable, CancellationReasonRequired, StaleVersion, WitnessRequired, WitnessNotVerified, BatchExpired) are reused,
/// never redeclared. The last block are codes this build needed beyond section 10.4 (see the handover Amended (build) row).</summary>
public static partial class BusinessViolationCodes
{
    public const string SurgeryNotFound = nameof(SurgeryNotFound);
    public const string InvalidSurgeryTransition = nameof(InvalidSurgeryTransition);
    public const string ServiceNotSurgical = nameof(ServiceNotSurgical);
    public const string ProcedureSexMismatch = nameof(ProcedureSexMismatch);
    public const string CatAlreadyNeutered = nameof(CatAlreadyNeutered);
    public const string SurgeonNotQualified = nameof(SurgeonNotQualified);
    public const string AssistantNotQualified = nameof(AssistantNotQualified);
    public const string AssistantSameAsSurgeon = nameof(AssistantSameAsSurgeon);
    public const string TheatreNotAvailable = nameof(TheatreNotAvailable);
    public const string SurgeonNotAvailable = nameof(SurgeonNotAvailable);
    public const string AssistantNotAvailable = nameof(AssistantNotAvailable);
    public const string SlotInPast = nameof(SlotInPast);
    public const string TheatreBusy = nameof(TheatreBusy);
    public const string SurgeonBusy = nameof(SurgeonBusy);
    public const string AssistantBusy = nameof(AssistantBusy);
    public const string StaffBusy = nameof(StaffBusy);
    public const string AdmissionNotToday = nameof(AdmissionNotToday);
    public const string ConsentNotRecorded = nameof(ConsentNotRecorded);
    public const string ConsentOverrideNotPermitted = nameof(ConsentOverrideNotPermitted);
    public const string ConsentOverrideReasonRequired = nameof(ConsentOverrideReasonRequired);
    public const string PreOpChecklistIncomplete = nameof(PreOpChecklistIncomplete);
    public const string ChecklistItemNotWaivable = nameof(ChecklistItemNotWaivable);
    public const string ChecklistItemAlreadyDone = nameof(ChecklistItemAlreadyDone);
    public const string WeightRecordNotValidForSurgery = nameof(WeightRecordNotValidForSurgery);
    public const string AnaesthesiaLogClosed = nameof(AnaesthesiaLogClosed);
    public const string ImplausibleVitalValue = nameof(ImplausibleVitalValue);
    public const string MedicineNotFound = nameof(MedicineNotFound);
    public const string BatchRequired = nameof(BatchRequired);
    public const string BatchSkuMismatch = nameof(BatchSkuMismatch);
    public const string AdministeringVetRequired = nameof(AdministeringVetRequired);
    public const string WitnessSameAsAdministrator = nameof(WitnessSameAsAdministrator);
    public const string WastageRequired = nameof(WastageRequired);
    public const string CorrectionTargetNotFound = nameof(CorrectionTargetNotFound);
    public const string RecordIsImmutable = nameof(RecordIsImmutable);
    public const string AnaesthesiaEntryAlreadyCorrected = nameof(AnaesthesiaEntryAlreadyCorrected);
    public const string DuplicateAnaesthesiaEntry = nameof(DuplicateAnaesthesiaEntry);
    public const string AnaesthesiaEntryRaced = nameof(AnaesthesiaEntryRaced);
    public const string AnaesthesiaEntryIdMismatch = nameof(AnaesthesiaEntryIdMismatch);
    public const string RecoveryHistoryLimitReached = nameof(RecoveryHistoryLimitReached);
    public const string ReportNotEditable = nameof(ReportNotEditable);
    public const string ReportAlreadyFinalised = nameof(ReportAlreadyFinalised);
    public const string ReportIncomplete = nameof(ReportIncomplete);
    public const string NotReadyForDischarge = nameof(NotReadyForDischarge);
    public const string PostOpInstructionsRequired = nameof(PostOpInstructionsRequired);
    public const string FollowUpOutsideWindow = nameof(FollowUpOutsideWindow);
    public const string FollowUpNotPending = nameof(FollowUpNotPending);
    public const string CampaignNotApplicable = nameof(CampaignNotApplicable);
    public const string CampaignServiceNotSterilisation = nameof(CampaignServiceNotSterilisation);
    public const string CampaignPriceLocked = nameof(CampaignPriceLocked);
    public const string AppointmentManagedBySurgery = nameof(AppointmentManagedBySurgery);

    // Beyond section 10.4 (build).
    public const string SurgeryProtocolInvalid = nameof(SurgeryProtocolInvalid);
    public const string SurgeryCampaignNotFound = nameof(SurgeryCampaignNotFound);
    public const string CampaignCodeAlreadyExists = nameof(CampaignCodeAlreadyExists);
    public const string SurgeryFollowUpNotFound = nameof(SurgeryFollowUpNotFound);
    public const string SurgicalReportNotFound = nameof(SurgicalReportNotFound);
    public const string InvalidAnaesthesiaEntry = nameof(InvalidAnaesthesiaEntry);
    public const string SurgeryCampaignInvalid = nameof(SurgeryCampaignInvalid);
    public const string ReportNotFinalised = nameof(ReportNotFinalised);
    public const string ChecklistItemNotManual = nameof(ChecklistItemNotManual);
    public const string SurgeryTextInvalid = nameof(SurgeryTextInvalid);
    public const string VeterinarianRequired = nameof(VeterinarianRequired);
}

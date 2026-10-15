namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-BRD-001 section 10.4 -- Boarding's own violation codes (ADR-341 per-area partial). <c>NoShowTooEarly</c>,
/// <c>AlreadyWaitlisted</c> and <c>CancellationReasonRequired</c> of the handover and the waitlist-not-found code are already declared by Appointments (and `InvalidSignature` by Consultation) and are reused
/// as the same strings.</summary>
public static partial class BusinessViolationCodes
{
    public const string BoardingUnitNotFound = nameof(BoardingUnitNotFound);
    public const string InvalidBoardingUnit = nameof(InvalidBoardingUnit);
    public const string InvalidMaintenanceBlock = nameof(InvalidMaintenanceBlock);
    public const string InvalidRateCard = nameof(InvalidRateCard);
    public const string InvalidCareRoutine = nameof(InvalidCareRoutine);
    public const string UnitCodeAlreadyExists = nameof(UnitCodeAlreadyExists);
    public const string UnitHasFutureStays = nameof(UnitHasFutureStays);
    public const string MaintenanceOverlapsStays = nameof(MaintenanceOverlapsStays);
    public const string MaintenanceOverlapsMaintenance = nameof(MaintenanceOverlapsMaintenance);
    public const string MaintenanceBlockNotFound = nameof(MaintenanceBlockNotFound);
    public const string RateCardCannotBeBackdated = nameof(RateCardCannotBeBackdated);
    public const string DuplicateRateCardEffectiveDate = nameof(DuplicateRateCardEffectiveDate);
    public const string UnitTypeNotPriced = nameof(UnitTypeNotPriced);
    public const string InvalidStayDates = nameof(InvalidStayDates);
    public const string StayTooLong = nameof(StayTooLong);
    public const string StayBeyondHorizon = nameof(StayBeyondHorizon);
    public const string ArrivalTooLate = nameof(ArrivalTooLate);
    public const string StayNotFound = nameof(StayNotFound);
    public const string InvalidStayTransition = nameof(InvalidStayTransition);
    public const string CatNotBoardable = nameof(CatNotBoardable);
    public const string DuplicateCatInStay = nameof(DuplicateCatInStay);
    public const string UnitCapacityExceeded = nameof(UnitCapacityExceeded);
    public const string NoUnitAvailable = nameof(NoUnitAvailable);
    public const string UnitNoLongerAvailable = nameof(UnitNoLongerAvailable);
    public const string CatAlreadyBoarding = nameof(CatAlreadyBoarding);
    public const string VaccinationGateBlocked = nameof(VaccinationGateBlocked);
    public const string OverrideReasonRequired = nameof(OverrideReasonRequired);
    public const string CareProfileIncomplete = nameof(CareProfileIncomplete);
    public const string InvalidCareProfile = nameof(InvalidCareProfile);
    public const string DepositNotYetReceived = nameof(DepositNotYetReceived);
    public const string ReservationNotLapsable = nameof(ReservationNotLapsable);
    public const string CheckInNotOnArrivalDate = nameof(CheckInNotOnArrivalDate);
    public const string CheckInChecklistIncomplete = nameof(CheckInChecklistIncomplete);
    public const string CheckOutChecklistIncomplete = nameof(CheckOutChecklistIncomplete);
    public const string UnitNotFreeForExtension = nameof(UnitNotFreeForExtension);
    public const string RelocationReasonRequired = nameof(RelocationReasonRequired);
    public const string IsolationUnitRequired = nameof(IsolationUnitRequired);
    public const string NoIsolationUnitAvailable = nameof(NoIsolationUnitAvailable);
    public const string NotInIsolation = nameof(NotInIsolation);
    public const string CareTaskNotFound = nameof(CareTaskNotFound);
    public const string CareTaskNotPending = nameof(CareTaskNotPending);
    public const string ObservationNotAllowed = nameof(ObservationNotAllowed);
    public const string IncidentNotFound = nameof(IncidentNotFound);
    public const string InvalidIncident = nameof(InvalidIncident);
    public const string IncidentStayNotInHouse = nameof(IncidentStayNotInHouse);
    public const string PhoneCallOutcomeRequired = nameof(PhoneCallOutcomeRequired);
    public const string ReferralStayNotInHouse = nameof(ReferralStayNotInHouse);
    public const string InvalidReferral = nameof(InvalidReferral);
    public const string WaitlistLimitReached = nameof(WaitlistLimitReached);
    public const string DaycareLimitReached = nameof(DaycareLimitReached);
    public const string VetAuthorityReasonRequired = nameof(VetAuthorityReasonRequired);
    public const string InvalidVetApproval = nameof(InvalidVetApproval);
    public const string VetApprovalNotFound = nameof(VetApprovalNotFound);
    public const string VetApprovalAlreadyDecided = nameof(VetApprovalAlreadyDecided);
    public const string InvalidMediaReference = nameof(InvalidMediaReference);
    public const string MediaStayNotInHouse = nameof(MediaStayNotInHouse);
    public const string StayMediaNotFound = nameof(StayMediaNotFound);
    public const string UnitsStillAvailable = nameof(UnitsStillAvailable);
    public const string DaycareNotOffered = nameof(DaycareNotOffered);
    public const string DaycareFull = nameof(DaycareFull);
    public const string DaycareVisitNotFound = nameof(DaycareVisitNotFound);
    public const string CatNotPresent = nameof(CatNotPresent);
    public const string StaleStayVersion = nameof(StaleStayVersion);
}

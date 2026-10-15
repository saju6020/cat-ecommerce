namespace CatEcommerce.Read.ViewModels;

// HO-PHM-001 §12.4 -- the portal's view of the owner's prescriptions and diet authorisations (US-PHM-07, US-PHM-08). The owner never sees a batch number, a safety record or
// another owner's anything.

/// <summary>
/// The repeat block of a portal prescription. <c>CanRequest</c> and <c>CannotRequestReason</c> come from the SAME rule function <c>RequestRepeat</c> uses
/// (<c>PrescriptionActionRules.EvaluateRepeat</c>), so the portal never shows a button the command would refuse.
/// </summary>
public sealed record CustomerRepeatViewModel(
    bool IsRepeatable, int SuppliesLeft, DateOnly ValidUntil, bool CanRequest, string? CannotRequestReason, string? CannotRequestCode, RepeatRequestViewModel? OpenRequest);

public record CustomerPrescriptionViewModel(
    Guid Id,
    Guid CatId,
    string CatName,
    PrescriptionMedicineViewModel Medicine,
    QuantityViewModel Dose,
    string Route,
    string Frequency,
    int RemainingDays,
    DateOnly CourseEndsOn,
    string IssuingVetName,
    DateTimeOffset IssuedOn,
    string Status,
    bool IsActive,
    CustomerRepeatViewModel Repeat);

/// <summary>A supply as the owner sees it: when, where and how much -- never the batch.</summary>
public sealed record CustomerDispensingViewModel(DateOnly DispensedOn, string BranchName, QuantityViewModel Quantity);

public sealed record CustomerPrescriptionDetailViewModel(
    Guid Id,
    Guid CatId,
    string CatName,
    PrescriptionMedicineViewModel Medicine,
    QuantityViewModel Dose,
    string Route,
    string Frequency,
    int RemainingDays,
    DateOnly CourseEndsOn,
    string IssuingVetName,
    DateTimeOffset IssuedOn,
    string Status,
    bool IsActive,
    CustomerRepeatViewModel Repeat,
    string Instructions,
    IReadOnlyList<CustomerDispensingViewModel> Dispensings,
    string BookLink)
    : CustomerPrescriptionViewModel(Id, CatId, CatName, Medicine, Dose, Route, Frequency, RemainingDays, CourseEndsOn, IssuingVetName, IssuedOn, Status, IsActive, Repeat);

public sealed record CustomerDietAuthorisationViewModel(
    Guid Id, Guid CatId, string CatName, Guid ProductId, string ProductName, DateOnly ValidFrom, DateOnly ValidTo, string Status, string AuthorisedByName);

/// <summary>A row the refill sweep turns into a <c>pharmacy.refill-due</c> request (§8.11).</summary>
public sealed record DueRefillReminderViewModel(
    Guid PrescriptionId, Guid CustomerId, Guid CatId, string CatName, string MedicineName, Guid BranchId, DateOnly RefillDueOn, DateOnly ValidUntil, int SupplyNumber);

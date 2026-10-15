using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Read.ViewModels;

// HO-EMR-001 §8.8 / §12.4 -- the discharge summary is DERIVED from a finalised visit and never stored (ADR-130).

public sealed record DischargeSummaryProblemViewModel(string Name, string Code, string Certainty);

public sealed record DischargeSummaryFollowUpViewModel(string Kind, DateOnly DueOn, DateTimeOffset? AppointmentStartsOn);

public sealed record DischargeSummaryCorrectionViewModel(DateTimeOffset AddedOn, string Text, string? Reason);

public sealed record DischargeSummarySectionViewModel(string Title, IReadOnlyList<string> Lines);

/// <summary>
/// The summary of a finalised visit, for staff and (own, finalised only) for the owner in the portal. <see cref="Treatments"/>
/// are the non-reversed treatment lines of the visit (description, dose, route, quantity and unit); <see cref="MedicinesAdministered"/> are the
/// medicine lines among them. Neither carries a price, a batch or a staff name.
/// <see cref="PrescriptionsAvailable"/> is false until HO-PHM-001 registers a contributor; the printed summary then says
/// that medicines to take home are on the prescription label.
/// </summary>
public sealed record DischargeSummaryViewModel(
    Guid VisitId,
    string BranchName, string BranchAddress, string? BranchPhone, string BranchTimeZoneId,
    Guid CatId, string CatName, string CatSex, string CatAge, Measurement? WeightAtVisit,
    DateOnly VisitDate, string AttendingVet,
    IReadOnlyList<DischargeSummaryProblemViewModel> Problems,
    IReadOnlyList<string> Treatments,
    IReadOnlyList<string> MedicinesAdministered,
    string HomeCareInstructions,
    DischargeSummaryFollowUpViewModel? FollowUp,
    IReadOnlyList<DischargeSummaryCorrectionViewModel> Corrections,
    IReadOnlyList<DischargeSummarySectionViewModel> ExtraSections,
    bool PrescriptionsAvailable);

/// <summary><c>GET /api/customers/me/cats/{catId}/visits</c> -- finalised visits only.</summary>
public sealed record CustomerVisitListItemViewModel(
    Guid Id, DateOnly Date, string Service, string Vet, IReadOnlyList<string> Problems, bool HasSummary,
    DateOnly? FollowUpOn);

/// <summary><c>GET /api/customers/me/cats/{catId}/lab-results</c> -- result sets of orders on <b>finalised</b> visits.</summary>
public sealed record CustomerLabResultViewModel(
    Guid OrderId, Guid VisitId, string Kind, DateTimeOffset OrderedOn,
    IReadOnlyList<DiagnosticResultSetViewModel> ResultSets, IReadOnlyList<DiagnosticAttachmentViewModel> Attachments);

namespace CatEcommerce.Read.ViewModels;

// HO-SRG-001 §12.4 -- the surgical report (US-SRG-05). It attaches to the cat AND the visit; once finalised only addenda can follow.

public sealed record SurgicalReportAddendumViewModel(Guid Id, string Text, Guid AddedBy, string AddedByName, DateTimeOffset AddedOn);

public sealed record SurgicalReportViewModel(
    Guid SurgeryId, Guid CatId, string CatName, Guid VisitId, Guid BranchId, string BranchTimeZoneId, int Version, string Status, string ProcedurePerformed, string Findings, string Technique,
    string SutureMaterial, bool HadComplications, string? Complications, Guid SurgeonId, string SurgeonName, Guid AssistantId, string AssistantName, DateTimeOffset? FinalisedOn,
    Guid? FinalisedBy, string? FinalisedByName, IReadOnlyList<SurgicalReportAddendumViewModel> Addenda);

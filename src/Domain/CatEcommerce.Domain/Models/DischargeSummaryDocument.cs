namespace CatEcommerce.Domain.Models;

/// <summary>HO-EMR-001 §8.8 -- the plain-data shape <c>IDischargeSummaryPdfRenderer</c> prints. Every
/// string is printed as text; none is ever interpreted as markup.</summary>
public sealed record DischargeSummaryDocument(
    string BranchName,
    string BranchAddress,
    string? BranchPhone,
    string CatName,
    string CatSex,
    string CatAge,
    string? WeightAtVisit,
    string VisitDateLocal,
    string AttendingVet,
    IReadOnlyList<DischargeSummaryProblem> Problems,
    string DischargeInstructions,
    string? FollowUp,
    IReadOnlyList<DischargeSummaryCorrection> Corrections,
    IReadOnlyList<DischargeSummarySection> ExtraSections,
    bool PrescriptionsAvailable);

public sealed record DischargeSummaryProblem(string Name, string Certainty);

public sealed record DischargeSummaryCorrection(string AddedOnLocal, string Text, string? Reason);

public sealed record DischargeSummarySection(string Title, IReadOnlyList<string> Lines);

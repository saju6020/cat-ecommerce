using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Visit;

// HO-EMR-001 §11.1 C7-C10 (Phase C). Data only; every visit write carries the caller's active BranchId.

/// <summary>C7 -- the per-author auto-saved working copy (ADR-122). No ExpectedVersion: last write wins
/// within one author's own copy. <see cref="DraftId"/> is used only when the draft is first created.</summary>
public sealed class SaveConsultationDraftCommand : Command
{
    public required Guid VisitId { get; init; }
    public required Guid DraftId { get; init; }
    public required Guid BranchId { get; init; }
    public string Subjective { get; init; } = "";
    public string Objective { get; init; } = "";
    public string Assessment { get; init; } = "";
    public string Plan { get; init; } = "";
    public string DischargeInstructions { get; init; } = "";
    public IReadOnlyList<Guid> ProblemCodeIds { get; init; } = [];
}

/// <summary>One coded problem of a note save (§11.1 C8).</summary>
public sealed class ProblemInput
{
    public required Guid ProblemId { get; init; }
    public required Guid DiagnosisCodeId { get; init; }
    public required string Certainty { get; init; }
    public string? Note { get; init; }
}

/// <summary>C8 -- an explicit save of the note and the coded problem list. A save with a stale
/// <see cref="ExpectedVersion"/> is a <c>409</c> (a concurrent save by another vet).</summary>
public sealed class SaveConsultationNoteCommand : Command
{
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
    public string Subjective { get; init; } = "";
    public string Objective { get; init; } = "";
    public string Assessment { get; init; } = "";
    public string Plan { get; init; } = "";
    public string DischargeInstructions { get; init; } = "";
    public IReadOnlyList<ProblemInput> Problems { get; init; } = [];
    public Guid? AppliedTemplateId { get; init; }
    public int? AppliedTemplateVersion { get; init; }
}

/// <summary>What <see cref="SaveConsultationNoteCommand"/> returns: the visit's new version and the save time.</summary>
public sealed record SaveNoteResult(int Version, DateTimeOffset SavedOn);

/// <summary>C9 -- finalise (Vet only, ADR-119). The handler also enqueues <c>visit.summary-available</c>.</summary>
public sealed class FinaliseVisitCommand : Command
{
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
}

/// <summary>C10 -- a correction to a finalised visit: separate, timestamped, attributed, append-only.</summary>
public sealed class AddVisitAddendumCommand : Command
{
    public required Guid AddendumId { get; init; }
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Text { get; init; }
    public string? Reason { get; init; }
}

using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Surgery;

/// <summary>HO-SRG-001 §11.1 C11 (US-SRG-05, US-SRG-06) -- the procedure ends: <c>Completed</c> or <c>Abandoned</c>. The report is NOT created here (C12 creates the draft on first save).</summary>
public sealed class EndSurgicalProcedureCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Outcome { get; init; }
}

/// <summary>C12 -- create or replace the DRAFT report. <c>ExpectedVersion</c> is required once a draft exists.</summary>
public sealed class SaveSurgicalReportDraftCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public int? ExpectedVersion { get; init; }
    public required string ProcedurePerformed { get; init; }
    public required string Findings { get; init; }
    public required string Technique { get; init; }
    public required string SutureMaterial { get; init; }
    public bool HadComplications { get; init; }
    public string? Complications { get; init; }
    public required Guid SurgeonId { get; init; }
    public required Guid AssistantId { get; init; }
}

public sealed class FinaliseSurgicalReportCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
}

public sealed class AddSurgicalReportAddendumCommand : Command
{
    public required Guid AddendumId { get; init; }
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Text { get; init; }
}

/// <summary>C13 (US-SRG-06, ADR-166) -- the repair path for the neuter status: idempotent, available from the surgery detail while <c>neuterStatusApplied</c> is false.</summary>
public sealed class ApplySterilisationOutcomeCommand : Command
{
    public required Guid SurgeryId { get; init; }
    public required Guid BranchId { get; init; }
}

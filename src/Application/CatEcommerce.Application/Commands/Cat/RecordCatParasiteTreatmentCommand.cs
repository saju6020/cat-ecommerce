using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Cat;

public sealed class RecordCatParasiteTreatmentCommand : Command
{
    public required Guid CatId { get; init; }
    public required Guid TreatmentId { get; init; }
    public required ParasiteTreatmentType TreatmentType { get; init; }
    public required string ProductName { get; init; }
    public string? BatchReference { get; init; }
    public required DateOnly AdministeredOn { get; init; }
    public required Guid AdministeredByStaffId { get; init; }
    public required int IntervalDays { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// HO-CAT-001 amendment, owner decision 2026-09-25 (HO-CAT-002-ACCEPTANCE BLOCKER 1 / A11 /
/// A13). The one <c>CommandResponse.Result</c> shape in this handover that is not a single id:
/// C10 has no read endpoint the API layer could otherwise re-fetch <see cref="NextDueOn"/> from
/// afterwards, so the handler returns both the created treatment's id and the
/// aggregate-computed, required next-due date. See CommandHandlers/Cat/
/// RecordCatParasiteTreatmentCommandHandler.cs and Controllers/CatsController.cs.
/// </summary>
public sealed record RecordCatParasiteTreatmentResult(Guid TreatmentId, DateOnly NextDueOn);

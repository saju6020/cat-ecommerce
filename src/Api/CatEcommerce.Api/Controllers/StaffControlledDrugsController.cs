using System.Text;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Prescription;
using CatEcommerce.Api.Filters;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-PHM-001 §12.4 (FR-PHM-7, C-2, BR-7) -- the controlled-drug register. Reading it is <c>ControlledDrugs.View</c> (Nurse, Vet, Inventory, Administrator);
/// correcting it is <c>ControlledDrugs.Correct</c> (Vet alone, OQ-8) and needs a verified witness, so it carries the <c>witness</c> rate limit. The register is per
/// branch (<c>X-Branch-Id</c>); an entry of another branch is a <c>404</c>. There is no endpoint that edits or deletes an entry -- a correction is a new, linked entry.
/// </summary>
[ApiController]
[Route("api/staff/controlled-drugs")]
public sealed class StaffControlledDrugsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : PharmacyControllerBase(activeBranchAccessor)
{
    [HttpGet("register")]
    [RequireAction(Actions.ControlledDrugs.View)]
    public async Task<IActionResult> GetRegister(
        [FromQuery] Guid? medicineId, [FromQuery] Guid? batchId, [FromQuery] string? kind, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!string.IsNullOrWhiteSpace(kind) && !EnumerationParsing.TryParse<ControlledDrugEntryKind>(kind, out _))
            return BadRequest(new { error = "kind is not a recognised entry kind." });

        var query = new GetControlledDrugRegisterQuery
        {
            BranchId = branchId, MedicineId = medicineId, BatchId = batchId, Kind = kind, From = from, To = to,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };

        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } paging) return paging;

        Response.Headers.CacheControl = "no-store";
        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    [HttpGet("register/{entryId:guid}")]
    [RequireAction(Actions.ControlledDrugs.View)]
    public async Task<IActionResult> GetEntry(Guid entryId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetControlledDrugEntryQuery
        { EntryId = entryId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        Response.Headers.CacheControl = "no-store";
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("register/{entryId:guid}/corrections")]
    [RequireAction(Actions.ControlledDrugs.Correct)]
    [EnableRateLimiting(PharmacyRateLimitPolicies.Witness)]
    public async Task<IActionResult> RecordCorrection(Guid entryId, [FromBody] RecordCorrectionRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var newEntryId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new RecordControlledDrugCorrectionCommand
        {
            EntryId = newEntryId, CorrectsEntryId = entryId, CorrectedDoseText = request.CorrectedDoseText, CorrectedCatId = request.CorrectedCatId,
            QuantityDelta = request.QuantityDelta, Notes = request.Notes, Reason = request.Reason, WitnessIdentifier = request.WitnessIdentifier,
            WitnessPassword = request.WitnessPassword, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return RespondToFailure(response, BusinessViolationCodes.RegisterEntryNotFound);

        var recorded = (RecordedRegisterEntry)response.Result!;
        return StatusCode(201, new { entryId = recorded.EntryId, entryNumber = recorded.EntryNumber });
    }

    [HttpGet("balances")]
    [RequireAction(Actions.ControlledDrugs.View)]
    public async Task<IActionResult> GetBalances()
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetControlledDrugBalancesQuery
        { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        Response.Headers.CacheControl = "no-store";
        return Ok(response.Result);
    }

    [HttpGet("verification")]
    [RequireAction(Actions.ControlledDrugs.View)]
    public async Task<IActionResult> Verify()
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new VerifyControlledDrugRegisterQuery
        { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        Response.Headers.CacheControl = "no-store";
        return Ok(response.Result);
    }

    /// <summary>An inspection export: CSV, injection-guarded (HO-ADM-001 ADR-051), at most 50,000 rows, <c>no-store</c> -- and audited.</summary>
    [HttpGet("register/export")]
    [RequireAction(Actions.ControlledDrugs.View)]
    public async Task<IActionResult> Export([FromQuery] Guid? medicineId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (from is { } start && to is { } end && (end < start || end.DayNumber - start.DayNumber > ControlledDrugRules.ExportMaxRangeDays))
            return BadRequest(new { error = $"The range must be at most {ControlledDrugRules.ExportMaxRangeDays} days and not end before it starts." });

        var userContext = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new ExportControlledDrugRegisterQuery
        {
            BranchId = branchId, MedicineId = medicineId, From = from, To = to, MaxRows = ControlledDrugRules.ExportMaxRows + 1,
            UserContext = userContext, CorrelationId = Guid.NewGuid()
        });

        var rows = response.Result;
        if (rows.Count > ControlledDrugRules.ExportMaxRows)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = $"The export is limited to {ControlledDrugRules.ExportMaxRows} rows; narrow the dates or pick a medicine." });

        var audit = await dispatcher.SendAsync(new RecordControlledDrugRegisterExportCommand
        { BranchId = branchId, MedicineId = medicineId, From = from, To = to, RowCount = rows.Count, UserContext = userContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        if (!audit.IsSuccess) return StatusCode(500, new { error = "The request could not be completed." });

        var csv = InventoryCsv.Build(
            ["EntryNumber", "RecordedOn", "OccurredOn", "Kind", "Medicine", "Strength", "Lot", "BatchExpiresOn", "QuantityDelta", "RunningBalance", "Unit", "DoseText", "Wastage",
                "Cat", "Actor", "Witness", "SourceType", "Source", "CorrectionReason", "Notes", "CorrectsEntryNumber", "CorrectedByEntryNumbers", "ChainPosition", "EntryHash"],
            rows.Select(r => (IReadOnlyList<object?>)new object?[]
            {
                r.EntryNumber, r.RecordedOn, r.OccurredOn, r.Kind, r.MedicineName, r.StrengthText, r.LotNumber, r.BatchExpiresOn, r.QuantityDelta, r.RunningBalance, r.Unit, r.DoseText,
                r.Wastage, r.CatName, r.ActorName, r.WitnessName, r.SourceType, r.SourceLabel, r.CorrectionReason, r.Notes, r.CorrectsEntryNumber, r.CorrectedByEntryNumbers,
                r.ChainPosition, r.EntryHash
            }));

        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "controlled-drug-register.csv");
    }
}

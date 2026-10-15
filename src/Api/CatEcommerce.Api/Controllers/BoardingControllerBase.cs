using System.Globalization;
using CatEcommerce.Api.Contracts.Boarding;
using CatEcommerce.Application.CommandHandlers.Boarding;
using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Read.QueryHandlers.Boarding;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-BRD-001 section 12.4 -- the shared plumbing of the Boarding controllers: the active branch (<c>X-Branch-Id</c>, HO-ADM-001 ADR-046) and the mapping of a
/// returned <see cref="CommandResponse"/> to HTTP: a "not found" violation is <c>404</c> (someone else's stay is never confirmed to exist), a lost race is
/// <c>409</c>, everything else <c>400</c> with the violation body. Never <c>202</c>.
/// </summary>
public abstract class BoardingControllerBase(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    protected IInMemoryDispatcher Dispatcher => dispatcher;
    protected UserContext Caller => userContextProvider.GetUserContext();

    private static readonly HashSet<string> NotFoundCodes =
    [
        BusinessViolationCodes.StayNotFound, BusinessViolationCodes.BoardingUnitNotFound, BusinessViolationCodes.MaintenanceBlockNotFound,
        BusinessViolationCodes.CareTaskNotFound, BusinessViolationCodes.IncidentNotFound, BusinessViolationCodes.VetApprovalNotFound,
        BusinessViolationCodes.StayMediaNotFound, BusinessViolationCodes.WaitlistEntryNotFound, BusinessViolationCodes.DaycareVisitNotFound,
        BusinessViolationCodes.CatNotFound,
    ];

    private static readonly HashSet<string> ConflictCodes =
    [
        BusinessViolationCodes.UnitNoLongerAvailable, BusinessViolationCodes.CatAlreadyBoarding, BusinessViolationCodes.StaleStayVersion, BusinessViolationCodes.DaycareFull,
    ];

    protected bool TryResolveBranch(out Guid branchId, out IActionResult? refusal)
    {
        var active = activeBranchAccessor.GetActiveBranchId();
        if (active.BranchId is { } id)
        {
            branchId = id;
            refusal = null;
            return true;
        }

        branchId = default;
        refusal = StatusCode(403, new { error = active.Refusal?.ToString() ?? "branch_required" });
        return false;
    }

    protected IActionResult Respond(CommandResponse response, Func<IActionResult> onSuccess) =>
        response.IsSuccess ? onSuccess() : RespondToFailure(response.ValidationResult);

    protected IActionResult RespondToFailure(ValidationResponse? validationResult)
    {
        var codes = validationResult?.Errors.Select(e => e.ErrorCode).ToHashSet(StringComparer.Ordinal) ?? [];
        if (codes.Overlaps(NotFoundCodes)) return NotFound();
        if (codes.Overlaps(ConflictCodes)) return Conflict(validationResult);
        return BadRequest(validationResult);
    }

    protected static bool TryParseMoney(string? text, out decimal amount) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);

    protected static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    /// <summary>The enquiry response (section 12.4): the stay id, the vaccination gate with each blocked cat's reasons and booking link, and the unstored quote.</summary>
    protected static object EnquiryBody(EnquiryResult result) => new
    {
        stayId = result.StayId,
        vaccinationGate = new
        {
            passed = result.Cats.All(c => c.Evaluation.Passed),
            blockedCats = result.Cats.Where(c => !c.Evaluation.Passed).Select(c => new
            {
                catId = c.CatId, catName = c.CatName, bookingLink = c.BookingLink,
                reasons = c.Evaluation.Reasons.Select(r => new { vaccineCode = r.VaccineCode, vaccineName = r.VaccineName, reason = r.Reason, dueOn = r.DueOn }),
            }),
        },
        quote = result.Quote is null ? null : QuoteMapping.ToViewModel(result.Quote, result.CurrencyCode),
    };

    protected static bool TryMapCareProfile(CareProfileRequest request, out CareProfileInput? input, out string? error)
    {
        input = null;
        error = null;

        var feedings = new List<FeedingInput>();
        foreach (var f in request.Feedings)
        {
            if (!TryParseTime(f.Time, out var time)) { error = $"'{f.Time}' is not a time (HH:mm)."; return false; }
            feedings.Add(new FeedingInput(time, f.Portion, f.FoodSource, f.FoodDescription, f.HouseFoodSkuId));
        }

        var medications = new List<MedicationInput>();
        foreach (var m in request.Medications)
        {
            var times = new List<TimeOnly>();
            foreach (var t in m.Times)
            {
                if (!TryParseTime(t, out var parsed)) { error = $"'{t}' is not a time (HH:mm)."; return false; }
                times.Add(parsed);
            }

            medications.Add(new MedicationInput(m.Name, m.Dose, m.Route, times, m.Instructions, m.FromOn, m.ToOn));
        }

        if (!TryParseMoney(request.VetSpendingLimit, out var limit)) { error = "Invalid amount."; return false; }
        if (request.EmergencyContact is null) { error = "An emergency contact is required."; return false; }

        input = new CareProfileInput(
            feedings, medications, request.LitterType, request.LitterNote, request.VetVisitAuthorised, limit,
            new EmergencyContactInput(request.EmergencyContact.Name, request.EmergencyContact.Phone, request.EmergencyContact.Relationship), request.Notes);
        return true;
    }
}

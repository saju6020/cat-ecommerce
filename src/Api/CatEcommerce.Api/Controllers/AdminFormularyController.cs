using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Prescription;
using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-PHM-001 §12.4 (US-PHM-04, ADR-138) -- an administrator maintains the formulary (<c>Formulary.Manage</c>). Ids are minted here; the
/// <c>201</c> carries the real result (never <c>202</c>). The controlled flag and the active flag are their own endpoints.</summary>
[ApiController]
[Route("api/admin/formulary")]
public sealed class AdminFormularyController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : PharmacyControllerBase(activeBranchAccessor)
{
    [HttpGet("sku-search")]
    [RequireAction(Actions.Formulary.Manage)]
    public async Task<IActionResult> SearchSkus([FromQuery] string? term)
    {
        var response = await dispatcher.GetResultAsync(new SearchFormularySkusQuery
        { Term = term, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Formulary.Manage)]
    public async Task<IActionResult> Create([FromBody] FormularyMedicineRequest request)
    {
        var medicineId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new CreateFormularyMedicineCommand
        {
            MedicineId = medicineId, Code = request.Code, Name = request.Name, ActiveIngredients = request.ActiveIngredients, DrugClasses = request.DrugClasses,
            Form = request.Form, Strength = ToModel(request.Strength), DefaultRoute = request.DefaultRoute, AllowedRoutes = request.AllowedRoutes,
            DosingBasis = request.DosingBasis, DoseRange = ToModel(request.DoseRange), Contraindications = request.Contraindications,
            Interactions = ToModels(request.Interactions), DispensingUnit = request.DispensingUnit, LinkedSkuIds = request.LinkedSkuIds,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { medicineId }) : RespondToFailure(response);
    }

    [HttpPut("{medicineId:guid}")]
    [RequireAction(Actions.Formulary.Manage)]
    public async Task<IActionResult> Update(Guid medicineId, [FromBody] FormularyMedicineRequest request)
    {
        var response = await dispatcher.SendAsync(new UpdateFormularyMedicineCommand
        {
            MedicineId = medicineId, ExpectedVersion = request.ExpectedVersion, Name = request.Name, ActiveIngredients = request.ActiveIngredients,
            DrugClasses = request.DrugClasses, Form = request.Form, Strength = ToModel(request.Strength), DefaultRoute = request.DefaultRoute,
            AllowedRoutes = request.AllowedRoutes, DosingBasis = request.DosingBasis, DoseRange = ToModel(request.DoseRange),
            Contraindications = request.Contraindications, Interactions = ToModels(request.Interactions), DispensingUnit = request.DispensingUnit,
            LinkedSkuIds = request.LinkedSkuIds, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.FormularyMedicineNotFound);
    }

    [HttpPut("{medicineId:guid}/controlled")]
    [RequireAction(Actions.Formulary.Manage)]
    public async Task<IActionResult> SetControlled(Guid medicineId, [FromBody] SetControlledRequest request)
    {
        var response = await dispatcher.SendAsync(new SetFormularyMedicineControlledCommand
        {
            MedicineId = medicineId, IsControlled = request.IsControlled, ControlledSchedule = request.ControlledSchedule, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.FormularyMedicineNotFound);
    }

    [HttpPut("{medicineId:guid}/active")]
    [RequireAction(Actions.Formulary.Manage)]
    public async Task<IActionResult> SetActive(Guid medicineId, [FromBody] SetActiveRequest request)
    {
        var response = await dispatcher.SendAsync(new SetFormularyMedicineActiveCommand
        {
            MedicineId = medicineId, IsActive = request.IsActive, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : RespondToFailure(response, BusinessViolationCodes.FormularyMedicineNotFound);
    }

    private static StrengthModel ToModel(StrengthRequest s) => new(s.Amount, s.Unit, s.PerQuantity, s.PerUnit);

    private static DoseRangeModel? ToModel(DoseRangeRequest? r) => r is null ? null : new DoseRangeModel(r.MinMgPerKg, r.MaxMgPerKg);

    private static List<InteractionModel> ToModels(IEnumerable<InteractionRequest> interactions) =>
        interactions.Select(i => new InteractionModel(i.InteractsWithMedicineId, i.InteractsWithTerm, i.Severity, i.Note)).ToList();
}

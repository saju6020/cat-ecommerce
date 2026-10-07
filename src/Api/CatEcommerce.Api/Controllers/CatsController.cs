using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Cat;
using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Infrastructure.Documents;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.SharedDto.Cat;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// Route/verb/status table -- HO-CAT-001 section 12.4. Mints ids server-side, stamps
/// UserContext/CorrelationId/ValidateCommand=true on every command (F3), dispatches exactly one
/// command or query per action. Never 202.
///
/// HO-ADM-001 §12.3 / ADR-043: every action now carries <c>[RequireAction(..., OwnerScope.Cat)]</c>
/// per the mapping table there. A customer caller reaching <see cref="RegisterCat"/> or
/// <see cref="GetCats"/> (no <c>catId</c> in the route) has their owner id **forced** to the caller
/// (F19) -- a client-supplied owner id is ignored for a customer.
/// </summary>
[ApiController]
[Route("api/cats")]
public sealed class CatsController(
    IInMemoryDispatcher dispatcher,
    IUserContextProvider userContextProvider,
    ICatMedicalHistoryPdfRenderer pdfRenderer,
    ICatQrCodeGenerator qrCodeGenerator,
    ILogger<CatsController> logger) : ControllerBase
{
    [HttpPost]
    [RequireAction(Actions.Cats.Register, OwnerScope.CatCollection)]
    public async Task<IActionResult> RegisterCat([FromBody] RegisterCatRequest request)
    {
        if (TryParseEnum<CatSex>(request.Sex, out var sex) is { } sexError) return sexError;
        if (TryParseEnum<DateOfBirthPrecision>(request.DateOfBirthPrecision, out var precision) is { } precisionError)
            return precisionError;

        var caller = CurrentCaller();
        var ownerId = caller.Kind == ResolvedCallerKind.Customer ? caller.AccountId : request.OwnerId;

        var command = new RegisterCatCommand
        {
            CatId = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = request.Name,
            Species = request.Species,
            Sex = sex!,
            DateOfBirth = request.DateOfBirth,
            DateOfBirthPrecision = precision!,
            Breed = request.Breed,
            ColourAndMarkings = request.ColourAndMarkings,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess
            ? CreatedAtAction(nameof(GetCat), new { catId = response.Result }, new { catId = response.Result })
            : BadRequest(response.ValidationResult);
    }

    [HttpGet]
    [RequireAction(Actions.Cats.View, OwnerScope.CatCollection)]
    public async Task<IActionResult> GetCats(
        [FromQuery] Guid? ownerId, [FromQuery] bool includeDeceased = false,
        [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null)
    {
        // F19 / §12.3: a customer-supplied ownerId is ignored -- the caller only ever sees their
        // own cats regardless of what the query string asks for (U-A4/I-A5).
        var caller = CurrentCaller();
        var effectiveOwnerId = caller.Kind == ResolvedCallerKind.Customer ? caller.AccountId : ownerId;

        var query = new GetCatsQuery
        {
            OwnerId = effectiveOwnerId,
            IncludeDeceased = includeDeceased,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } pagingError) return pagingError;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("{catId:guid}")]
    [RequireAction(Actions.Cats.View, OwnerScope.Cat)]
    public async Task<IActionResult> GetCat(Guid catId)
    {
        var response = await dispatcher.GetResultAsync(new GetCatProfileQuery
        {
            CatId = catId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPut("{catId:guid}/profile")]
    [RequireAction(Actions.Cats.EditProfile, OwnerScope.Cat)]
    public async Task<IActionResult> UpdateCatProfile(Guid catId, [FromBody] UpdateCatProfileRequest request)
    {
        if (TryParseEnum<CatSex>(request.Sex, out var sex) is { } sexError) return sexError;
        if (TryParseEnum<DateOfBirthPrecision>(request.DateOfBirthPrecision, out var precision) is { } precisionError)
            return precisionError;

        var command = new UpdateCatProfileCommand
        {
            CatId = catId,
            Name = request.Name,
            Sex = sex!,
            DateOfBirth = request.DateOfBirth,
            DateOfBirthPrecision = precision!,
            Breed = request.Breed,
            ColourAndMarkings = request.ColourAndMarkings,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? Ok(new { catId }) : BadRequest(response.ValidationResult);
    }

    [HttpPut("{catId:guid}/neuter-status")]
    [RequireAction(Actions.Cats.RecordClinical)]
    public async Task<IActionResult> RecordCatNeuterStatus(Guid catId, [FromBody] RecordCatNeuterStatusRequest request)
    {
        if (TryParseEnum<NeuterStatus>(request.NeuterStatus, out var neuterStatus) is { } error) return error;

        var command = new RecordCatNeuterStatusCommand
        {
            CatId = catId,
            NeuterStatus = neuterStatus!,
            NeuteredOn = request.NeuteredOn,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? Ok(new { catId }) : BadRequest(response.ValidationResult);
    }

    [HttpPut("{catId:guid}/medical-profile")]
    [RequireAction(Actions.Cats.RecordClinical)]
    public async Task<IActionResult> UpdateCatMedicalProfile(Guid catId, [FromBody] UpdateCatMedicalProfileRequest request)
    {
        var allergies = new List<AllergyInput>();
        foreach (var a in request.Allergies)
        {
            if (TryParseEnum<AllergySeverity>(a.Severity, out var severity) is { } error) return error;
            allergies.Add(new AllergyInput(a.Allergen, a.Reaction, severity!, a.RecordedOn));
        }

        var command = new UpdateCatMedicalProfileCommand
        {
            CatId = catId,
            Allergies = allergies,
            ChronicConditions = request.ChronicConditions
                .Select(c => new ChronicConditionInput(c.Condition, c.DiagnosedOn, c.Notes)).ToList(),
            Medications = request.Medications
                .Select(m => new MedicationInput(m.MedicationName, m.Dosage, m.Frequency, m.StartedOn, m.EndsOn)).ToList(),
            DietaryNotes = request.DietaryNotes,
            BehaviouralNotes = request.BehaviouralNotes,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? Ok(new { catId }) : BadRequest(response.ValidationResult);
    }

    [HttpPost("{catId:guid}/photos")]
    [RequireAction(Actions.Cats.EditProfile, OwnerScope.Cat)]
    public async Task<IActionResult> AddCatPhoto(Guid catId, [FromBody] AddCatPhotoRequest request)
    {
        var photoId = Guid.NewGuid();
        var command = new AddCatPhotoCommand
        {
            CatId = catId,
            PhotoId = photoId,
            FileId = request.FileId,
            IsPrimary = request.IsPrimary,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess
            ? CreatedAtAction(nameof(GetCat), new { catId }, new { catId, photoId })
            : BadRequest(response.ValidationResult);
    }

    [HttpPut("{catId:guid}/high-risk-flag")]
    [RequireAction(Actions.Cats.ManageHighRiskFlag)]
    public async Task<IActionResult> FlagCatHighRisk(Guid catId, [FromBody] FlagCatHighRiskRequest request)
    {
        var command = new FlagCatHighRiskCommand
        {
            CatId = catId,
            HandlingNote = request.HandlingNote,
            Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? Ok(new { catId }) : BadRequest(response.ValidationResult);
    }

    [HttpDelete("{catId:guid}/high-risk-flag")]
    [RequireAction(Actions.Cats.ManageHighRiskFlag)]
    public async Task<IActionResult> ClearCatHighRiskFlag(Guid catId, [FromBody] ClearCatHighRiskFlagRequest request)
    {
        var command = new ClearCatHighRiskFlagCommand
        {
            CatId = catId,
            Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? Ok(new { catId }) : BadRequest(response.ValidationResult);
    }

    [HttpPost("{catId:guid}/weights")]
    [RequireAction(Actions.Cats.RecordClinical)]
    public async Task<IActionResult> RecordCatWeight(Guid catId, [FromBody] RecordCatWeightRequest request)
    {
        var weightRecordId = Guid.NewGuid();
        var command = new RecordCatWeightCommand
        {
            WeightRecordId = weightRecordId,
            CatId = catId,
            Kilograms = request.Kilograms,
            RecordedOn = request.RecordedOn,
            // SECURITY gate, MEDIUM (2026-09-26): always the caller, never client-supplied.
            RecordedByStaffId = userContextProvider.GetUserContext().UserId,
            Notes = request.Notes,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess
            ? CreatedAtAction(nameof(GetCatWeightHistory), new { catId }, new { catId, weightRecordId })
            : BadRequest(response.ValidationResult);
    }

    [HttpGet("{catId:guid}/weights")]
    [RequireAction(Actions.Cats.View, OwnerScope.Cat)]
    public async Task<IActionResult> GetCatWeightHistory(
        Guid catId, [FromQuery] int? pageNumber = null, [FromQuery] int? pageSize = null)
    {
        var query = new GetCatWeightHistoryQuery
        {
            CatId = catId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } pagingError) return pagingError;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpPost("{catId:guid}/vaccinations")]
    [RequireAction(Actions.Cats.RecordClinical)]
    public async Task<IActionResult> RecordCatVaccination(Guid catId, [FromBody] RecordCatVaccinationRequest request)
    {
        var vaccinationId = Guid.NewGuid();
        var command = new RecordCatVaccinationCommand
        {
            CatId = catId,
            VaccinationId = vaccinationId,
            VaccineId = request.VaccineId,
            BatchReference = request.BatchReference,
            BatchExpiresOn = request.BatchExpiresOn,
            AdministeredOn = request.AdministeredOn,
            AdministeredByVetId = request.AdministeredByVetId,
            Notes = request.Notes,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess
            ? CreatedAtAction(nameof(GetCatVaccinationStatus), new { catId }, new { catId, vaccinationId })
            : BadRequest(response.ValidationResult);
    }

    [HttpGet("{catId:guid}/vaccination-status")]
    [RequireAction(Actions.Cats.View, OwnerScope.Cat)]
    public async Task<IActionResult> GetCatVaccinationStatus(Guid catId)
    {
        var response = await dispatcher.GetResultAsync(new GetCatVaccinationStatusQuery
        {
            CatId = catId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("{catId:guid}/parasite-treatments")]
    [RequireAction(Actions.Cats.RecordClinical)]
    public async Task<IActionResult> RecordCatParasiteTreatment(Guid catId, [FromBody] RecordCatParasiteTreatmentRequest request)
    {
        if (TryParseEnum<ParasiteTreatmentType>(request.TreatmentType, out var treatmentType) is { } error) return error;

        var treatmentId = Guid.NewGuid();
        var command = new RecordCatParasiteTreatmentCommand
        {
            CatId = catId,
            TreatmentId = treatmentId,
            TreatmentType = treatmentType!,
            ProductName = request.ProductName,
            BatchReference = request.BatchReference,
            AdministeredOn = request.AdministeredOn,
            AdministeredByStaffId = request.AdministeredByStaffId,
            IntervalDays = request.IntervalDays,
            Notes = request.Notes,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        if (!response.IsSuccess) return BadRequest(response.ValidationResult);

        // C10's success body is the one exception to "{ <resource>Id } only" (owner decision
        // 2026-09-25, HO-CAT-001 §12.4): exactly { treatmentId, nextDueOn }, no catId -- the
        // client already knows the cat's id, it's in the URL.
        var result = (RecordCatParasiteTreatmentResult)response.Result!;
        return CreatedAtAction(nameof(GetCat), new { catId },
            new { treatmentId = result.TreatmentId, nextDueOn = result.NextDueOn });
    }

    [HttpPost("{catId:guid}/deceased")]
    [RequireAction(Actions.Cats.MarkDeceased)]
    public async Task<IActionResult> MarkCatDeceased(Guid catId, [FromBody] MarkCatDeceasedRequest request)
    {
        var command = new MarkCatDeceasedCommand
        {
            CatId = catId,
            DeceasedOn = request.DeceasedOn,
            Note = request.Note,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? Ok(new { catId }) : BadRequest(response.ValidationResult);
    }

    [HttpPost("{catId:guid}/ownership-transfer")]
    [RequireAction(Actions.Cats.TransferOwnership)]
    public async Task<IActionResult> TransferCatOwnership(Guid catId, [FromBody] TransferCatOwnershipRequest request)
    {
        var command = new TransferCatOwnershipCommand
        {
            CatId = catId,
            NewOwnerId = request.NewOwnerId,
            Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? Ok(new { catId }) : BadRequest(response.ValidationResult);
    }

    [HttpGet("{catId:guid}/medical-history.pdf")]
    [RequireAction(Actions.Cats.View, OwnerScope.Cat)]
    public async Task<IActionResult> GetCatMedicalHistoryPdf(Guid catId)
    {
        var response = await dispatcher.GetResultAsync(new GetCatMedicalHistoryQuery
        {
            CatId = catId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        if (response.Result is null) return NotFound();

        var viewModel = response.Result;
        var document = new CatMedicalHistoryDocument(
            viewModel.CatId, viewModel.Name, viewModel.OwnerId, viewModel.Species, viewModel.Breed,
            viewModel.Sex, viewModel.DateOfBirth, viewModel.DateOfBirthPrecision,
            viewModel.NeuterStatus, viewModel.NeuteredOn, viewModel.IsHighRisk, viewModel.HandlingNote,
            viewModel.Vaccinations.Select(v => new CatMedicalHistoryDocumentVaccination(
                v.VaccineName, v.BatchReference, v.AdministeredOn, v.NextDueOn)).ToList(),
            viewModel.ParasiteTreatments.Select(p => new CatMedicalHistoryDocumentParasiteTreatment(
                p.TreatmentType, p.ProductName, p.AdministeredOn, p.NextDueOn)).ToList(),
            viewModel.WeightHistory.Select(w => new CatMedicalHistoryDocumentWeightPoint(
                w.RecordedOn, w.Kilograms)).ToList());

        byte[] pdfBytes;
        try
        {
            pdfBytes = pdfRenderer.Render(document);
        }
        catch (CatMedicalHistoryRenderException)
        {
            // SECURITY gate, LOW (2026-09-26): the underlying QuestPDF exception's own Message
            // quotes the offending free text verbatim (a cat's name, breed, or note) -- passing
            // the exception itself to the logger (or letting it bubble to
            // SafeExceptionHandlerMiddleware, which does) would put that text in the log. Log the
            // cat id only; never the exception object or its message.
            //
            // ARCHITECTURE/CODE_REVIEW gates, LOW (2026-09-26, second pass): catches the port-level
            // CatMedicalHistoryRenderException, not QuestPDF's own DocumentDrawingException --
            // QuestPdfCatMedicalHistoryRenderer translates it, so the Api layer never has to know
            // which PDF library sits behind ICatMedicalHistoryPdfRenderer.
            logger.LogError("Medical history PDF rendering failed for cat {CatId}", catId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        // SEC hardening (optional, gate fix 2026-09-26, second pass): a clinical record document,
        // not meant to be cached by an intermediary or the browser.
        Response.Headers.CacheControl = "no-store";
        return File(pdfBytes, "application/pdf", $"{viewModel.Name}-medical-history.pdf");
    }

    [HttpGet("{catId:guid}/cage-card")]
    [RequireAction(Actions.Cats.PrintCageCard)]
    public async Task<IActionResult> GetCatCageCard(Guid catId)
    {
        var response = await dispatcher.GetResultAsync(new GetCatCageCardQuery
        {
            CatId = catId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        if (response.Result is null) return NotFound();

        var qrBytes = qrCodeGenerator.Generate(response.Result.ProfileUrl);

        // SEC hardening (optional, gate fix 2026-09-26, second pass): not meant to be cached by an
        // intermediary or the browser.
        Response.Headers.CacheControl = "no-store";
        return File(qrBytes, "image/png");
    }

    /// <summary>HO-ADM-001 §12.3 / F19: the resolved caller, for owner-forcing on register/list.</summary>
    private ResolvedCaller CurrentCaller() =>
        HttpContext.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller ?? ResolvedCaller.Anonymous;

    /// <summary>
    /// Request DTOs carry enum values as plain strings (System.Text.Json has no built-in
    /// converter for the Enumeration base class); this maps to the matching member by
    /// DisplayName and returns a 400 IActionResult on no match, or null when parsing succeeded.
    ///
    /// ARCHITECTURE/CODE_REVIEW gates, LOW (2026-09-26): the 400 body is the same
    /// <c>CommandResponse.ValidationResult</c> shape every other validation failure returns
    /// (ADR-014), not a bespoke <c>{ error }</c> object. `web/lib/api/client.ts` parses every 400
    /// body as that shape unconditionally, so the previous ad hoc shape would have thrown inside
    /// `ApiValidationError` handling had the web client ever hit this path.
    /// </summary>
    private IActionResult? TryParseEnum<T>(string value, out T? result) where T : Enumeration
    {
        var match = Enumeration.GetAll<T>()
            .FirstOrDefault(e => string.Equals(e.DisplayName, value, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            result = null;
            var validationResponse = new ValidationResponse();
            validationResponse.AddError(
                errorMessage: $"'{value}' is not a valid {typeof(T).Name}.",
                propertyName: typeof(T).Name,
                errorCode: BusinessViolationCodes.InvalidEnumValue,
                resourceName: typeof(T).Name);
            return BadRequest(validationResponse);
        }

        result = match;
        return null;
    }
}

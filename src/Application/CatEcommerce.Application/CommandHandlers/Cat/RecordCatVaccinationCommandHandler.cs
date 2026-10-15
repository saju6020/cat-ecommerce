using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Vaccination;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Cat;

/// <summary>
/// Canonical handler shape -- HO-CAT-001 section 11.5. StaffAccountAggregate is a second
/// read-only lookup, the same pattern as VaccineAggregate: AdministeredByVetId names a colleague
/// (SECURITY gate, MEDIUM, 2026-09-26), so it must resolve to an existing, active staff account
/// holding the Vet role -- not merely whatever guid the client sent.
/// </summary>
public sealed class RecordCatVaccinationCommandHandler(
    ILogger<RecordCatVaccinationCommandHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<VaccineAggregate> vaccineRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffAccountRepository,
    ICatRecordClock clock)
    : ICommandHandlerAsync<RecordCatVaccinationCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordCatVaccinationCommand command)
    {
        logger.LogInformation("Recording vaccination for cat {CatId}. CorrelationId: {CorrelationId}", command.CatId, command.CorrelationId);
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null)
            return Violation(BusinessViolationCodes.CatNotFound, nameof(command.CatId),
                $"Cat '{command.CatId}' was not found.");

        // VaccineAggregate is a read-only lookup. It is NOT mutated -- one aggregate per command.
        var vaccine = await vaccineRepository.GetByIdAsync(command.VaccineId);
        if (vaccine is null)
            return Violation(BusinessViolationCodes.VaccineNotFound, nameof(command.VaccineId),
                $"Vaccine '{command.VaccineId}' was not found.");
        if (!vaccine.IsActive)
            return Violation(BusinessViolationCodes.InactiveVaccine, nameof(command.VaccineId),
                $"Vaccine '{vaccine.Code}' is not active.");

        // StaffAccountAggregate is also a read-only lookup: GetByIdAsync already excludes
        // soft-deleted ("inactive") rows (EfAggregateRootRepository filters !IsMarkedToDelete).
        var vet = await staffAccountRepository.GetByIdAsync(command.AdministeredByVetId);
        if (vet is null)
            return Violation(BusinessViolationCodes.StaffAccountNotFound, nameof(command.AdministeredByVetId),
                $"Staff account '{command.AdministeredByVetId}' was not found.");
        if (!vet.RoleIds.Contains(StaffRoles.Vet))
            return Violation(BusinessViolationCodes.StaffAccountNotVeterinarian, nameof(command.AdministeredByVetId),
                "The administering staff member must hold the Vet role.");

        try
        {
            // IsCore + ValidityMonths travel in the DTO; the AGGREGATE computes NextDueOn.
            var today = await clock.LocalTodayAsync(CancellationToken.None);
            cat.RecordVaccination(command.ToDomainDto(vaccine, today));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = command.VaccinationId };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property,
            errorCode: code, resourceName: nameof(CatAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}

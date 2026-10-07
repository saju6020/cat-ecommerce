using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Cat;

/// <summary>
/// StaffAccountAggregate is a read-only lookup, the same pattern RecordCatVaccinationCommandHandler
/// uses for the vet field: AdministeredByStaffId names a colleague (SECURITY gate, MEDIUM,
/// 2026-09-26), so it must resolve to an existing, active staff account. Unlike the vet field,
/// any staff role is acceptable here -- deworming is not vet-exclusive (§10.2 Appendix D).
/// </summary>
public sealed class RecordCatParasiteTreatmentCommandHandler(
    ILogger<RecordCatParasiteTreatmentCommandHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffAccountRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordCatParasiteTreatmentCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordCatParasiteTreatmentCommand command)
    {
        logger.LogInformation("Recording parasite treatment for cat {CatId}. CorrelationId: {CorrelationId}", command.CatId, command.CorrelationId);
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null)
            return Violation(BusinessViolationCodes.CatNotFound, nameof(command.CatId),
                $"Cat '{command.CatId}' was not found.");

        var staffMember = await staffAccountRepository.GetByIdAsync(command.AdministeredByStaffId);
        if (staffMember is null)
            return Violation(BusinessViolationCodes.StaffAccountNotFound, nameof(command.AdministeredByStaffId),
                $"Staff account '{command.AdministeredByStaffId}' was not found.");

        try
        {
            var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            cat.RecordParasiteTreatment(command.ToDomainDto(today));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);

        // Select by the controller-minted id (HO-CAT-001 §11.5) rather than list order.
        var treatment = cat.ParasiteTreatments.Single(t => t.Id == command.TreatmentId);
        return new CommandResponse
        {
            IsSuccess = true,
            Result = new RecordCatParasiteTreatmentResult(treatment.Id, treatment.NextDueOn)
        };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property,
            errorCode: code, resourceName: nameof(CatAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}

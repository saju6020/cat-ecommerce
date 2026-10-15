using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Surgery.SurgeryHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Surgery;

/// <summary>
/// HO-SRG-001 §11.4 C1 (US-SRG-07, ADR-167) -- one protocol per surgical service (upsert). The service must exist and be a theatre procedure
/// (ADR-172); the follow-up windows are an aggregate rule.
/// </summary>
public sealed class UpsertSurgeryProtocolCommandHandler(
    IAggregateRootRepository<SurgeryProtocolAggregate> protocolRepository,
    IAggregateRootRepository<ClinicServiceAggregate> serviceRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<UpsertSurgeryProtocolCommand>
{
    public async Task<CommandResponse> HandleAsync(UpsertSurgeryProtocolCommand command)
    {
        var service = await serviceRepository.GetByIdAsync(command.ServiceId);
        if (service is null || service.RequiredRoomKind != RoomKind.Theatre)
            return Violation(BusinessViolationCodes.ServiceNotSurgical, nameof(command.ServiceId), SurgeryMessages.ServiceNotSurgical, "SurgeryProtocolAggregate");

        var existing = await protocolRepository.GetByFilterAsync(p => p.ProtocolServiceId == command.ServiceId);
        var protocol = existing ?? new SurgeryProtocolAggregate(Guid.NewGuid());

        try
        {
            protocol.Upsert(command.ToDomainDto(timeProvider.GetUtcNow()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex, "SurgeryProtocolAggregate");
        }

        if (existing is null) await protocolRepository.SaveAsync(protocol, EventPublishOption.InMemoryPublishOnly);
        else await protocolRepository.UpdateAsync(protocol, EventPublishOption.InMemoryPublishOnly);

        return Success(protocol.Id);
    }
}

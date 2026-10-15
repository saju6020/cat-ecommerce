using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-SRG-001 §11.3 -- command to domain DTO. Enum text has already been checked by the command validator.</summary>
public static class SurgeryMapper
{
    public static SurgeryProtocolDto ToDomainDto(this UpsertSurgeryProtocolCommand command, DateTimeOffset now) =>
        new(command.ServiceId, command.FastingInstructions, command.FastingHours, command.PostOpInstructions,
            command.FollowUps.Select(f => new FollowUpRule(AppointmentMapper.ParseEnum<SurgeryFollowUpKind>(f.Kind), f.DayOffset, f.WindowFromDay, f.WindowToDay)).ToList(),
            now, command.UserContext!);
}

using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Surgery;

/// <summary>HO-SRG-001 §11.1 C1. Data only; <c>ValidateCommand = true</c> everywhere.</summary>
public sealed class UpsertSurgeryProtocolCommand : Command
{
    public required Guid ServiceId { get; init; }
    public required string FastingInstructions { get; init; }
    public required int FastingHours { get; init; }
    public required string PostOpInstructions { get; init; }
    public required IReadOnlyList<SurgeryProtocolFollowUpInput> FollowUps { get; init; }
}

public sealed record SurgeryProtocolFollowUpInput(string Kind, int DayOffset, int WindowFromDay, int WindowToDay);

using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Administration;

public sealed class CreateClinicServiceCommand : Command
{
    public required Guid ServiceId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required int DurationMinutes { get; init; }
    public required IReadOnlyList<string> QualifiedRoleIds { get; init; }
    public required string RequiredRoomKind { get; init; }
    public required Guid TaxClassId { get; init; }
    public required bool IsBookableOnline { get; init; }
}

public sealed class UpdateClinicServiceCommand : Command
{
    public required Guid ServiceId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required int DurationMinutes { get; init; }
    public required IReadOnlyList<string> QualifiedRoleIds { get; init; }
    public required string RequiredRoomKind { get; init; }
    public required Guid TaxClassId { get; init; }
    public required bool IsBookableOnline { get; init; }
}

public sealed class SetClinicServiceActivationCommand : Command
{
    public required Guid ServiceId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class SetServicePriceCommand : Command
{
    public required Guid PriceId { get; init; }
    public required Guid ServiceId { get; init; }
    public required decimal Amount { get; init; }
    public required string CurrencyCode { get; init; }
    public required DateTimeOffset EffectiveFrom { get; init; }
}

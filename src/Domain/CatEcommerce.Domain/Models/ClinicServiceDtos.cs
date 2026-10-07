using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record ClinicServiceCreateDto(
    string Code, string Name, string Description, ServiceCategory Category, int DurationMinutes,
    IReadOnlyList<string> QualifiedRoleIds, RoomKind RequiredRoomKind, Guid TaxClassId, bool IsBookableOnline,
    UserContext UserContext, Guid CorrelationId);

public sealed record ClinicServiceUpdateDto(
    string Name, string Description, ServiceCategory Category, int DurationMinutes,
    IReadOnlyList<string> QualifiedRoleIds, RoomKind RequiredRoomKind, Guid TaxClassId, bool IsBookableOnline,
    UserContext UserContext, Guid CorrelationId);

public sealed record ClinicServiceActivationDto(bool IsActive, UserContext UserContext, Guid CorrelationId);

public sealed record ServicePriceCreateDto(
    Guid ServiceId, Money Amount, DateTimeOffset EffectiveFrom, Guid SetBy, DateTimeOffset Now,
    UserContext UserContext, Guid CorrelationId);

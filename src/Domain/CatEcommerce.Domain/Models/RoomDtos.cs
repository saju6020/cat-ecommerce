using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record RoomCreateDto(Guid BranchId, string Name, RoomKind Kind, UserContext UserContext, Guid CorrelationId);

public sealed record RoomUpdateDto(string Name, RoomKind Kind, bool IsActive, UserContext UserContext, Guid CorrelationId);

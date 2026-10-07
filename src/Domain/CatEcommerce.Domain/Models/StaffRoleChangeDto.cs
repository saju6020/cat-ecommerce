using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record StaffRoleChangeDto(IReadOnlyList<string> RoleIds, UserContext UserContext, Guid CorrelationId);

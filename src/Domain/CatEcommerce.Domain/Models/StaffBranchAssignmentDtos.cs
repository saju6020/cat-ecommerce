using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record StaffBranchAssignDto(
    IReadOnlyList<Guid> BranchIds, Guid PrimaryBranchId, UserContext UserContext, Guid CorrelationId);

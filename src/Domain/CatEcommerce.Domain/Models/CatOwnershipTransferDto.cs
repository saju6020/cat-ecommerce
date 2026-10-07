using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record CatOwnershipTransferDto(
    Guid NewOwnerId,
    string Reason,
    UserContext UserContext,
    Guid CorrelationId);

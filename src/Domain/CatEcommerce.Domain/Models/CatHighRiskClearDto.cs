using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record CatHighRiskClearDto(
    string Reason,
    UserContext UserContext,
    Guid CorrelationId);

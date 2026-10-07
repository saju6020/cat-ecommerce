using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record CatHighRiskFlagDto(
    string HandlingNote,
    string Reason,
    UserContext UserContext,
    Guid CorrelationId);

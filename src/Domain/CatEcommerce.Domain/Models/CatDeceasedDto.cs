using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record CatDeceasedDto(
    DateOnly DeceasedOn,
    string? Note,
    DateOnly Today,
    UserContext UserContext,
    Guid CorrelationId);

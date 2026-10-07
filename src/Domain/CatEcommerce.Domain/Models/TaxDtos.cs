using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record TaxClassCreateDto(string Code, string Name, UserContext UserContext, Guid CorrelationId);

public sealed record TaxClassUpdateDto(string Name, bool IsActive, UserContext UserContext, Guid CorrelationId);

public sealed record TaxRateCreateDto(
    Guid TaxClassId, decimal RatePercent, DateTimeOffset EffectiveFrom, DateTimeOffset Now,
    UserContext UserContext, Guid CorrelationId);

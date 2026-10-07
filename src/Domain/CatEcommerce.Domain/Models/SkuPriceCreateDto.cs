using CatEcommerce.Domain.ValueObjects;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-SHP-001 section 8.5 / section 11.1 C6.</summary>
public sealed record SkuPriceCreateDto(
    Guid SkuId,
    Money Amount,
    DateTimeOffset EffectiveFrom,
    Guid SetBy,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId);

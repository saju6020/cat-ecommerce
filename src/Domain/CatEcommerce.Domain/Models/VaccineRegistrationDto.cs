using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record VaccineRegistrationDto(
    string Code,
    string Name,
    bool IsCore,
    int ValidityMonths,
    string? Description,
    UserContext UserContext,
    Guid CorrelationId);

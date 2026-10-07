using CatEcommerce.Domain.ValueObjects;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record StaffAccountCreationDto(
    string FullName,
    MobileNumber? MobileNumber,
    EmailAddress? EmailAddress,
    IReadOnlyList<string> RoleIds,
    UserContext UserContext,
    Guid CorrelationId);

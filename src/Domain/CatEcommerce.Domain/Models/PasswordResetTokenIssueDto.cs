using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record PasswordResetTokenIssueDto(
    AccountKind AccountKind,
    Guid AccountId,
    string TokenHash,
    Guid IssuedForSecurityStamp,
    DateTimeOffset IssuedOn,
    DateTimeOffset ExpiresOn,
    PasswordResetChannel Channel,
    UserContext UserContext,
    Guid CorrelationId);

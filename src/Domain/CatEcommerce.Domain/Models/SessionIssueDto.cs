using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-ACC-001 §8.4. Shared shape for both <c>CustomerSessionAggregate.Issue</c> and
/// <c>StaffSessionAggregate.Issue</c> -- the mechanism is identical, only the policy (lifetime) differs.</summary>
public sealed record SessionIssueDto(
    Guid SubjectAccountId,
    string TokenHash,
    Guid IssuedForSecurityStamp,
    DateTimeOffset IssuedOn,
    DateTimeOffset ExpiresOn,
    string? IpAddress,
    string? UserAgent,
    UserContext UserContext,
    Guid CorrelationId,
    string CsrfTokenHash);

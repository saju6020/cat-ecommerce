using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record PolicyDocumentCreateDto(
    string Key, string Title, bool RequiresAcceptance, UserContext UserContext, Guid CorrelationId);

public sealed record PolicyDocumentSetCurrentVersionDto(int VersionNumber, UserContext UserContext, Guid CorrelationId);

public sealed record PolicyVersionCreateDto(
    string PolicyKey, int VersionNumber, string Body, DateTimeOffset PublishedOn, string ChangeSummary,
    bool RequiresAcceptance, UserContext UserContext, Guid CorrelationId);

public sealed record PolicyAcceptanceCreateDto(
    Guid CustomerAccountId, string PolicyKey, int VersionNumber, DateTimeOffset AcceptedOn, string? IpAddress,
    UserContext UserContext, Guid CorrelationId);

public sealed record PolicyAcceptanceIpRedactDto(UserContext UserContext, Guid CorrelationId);

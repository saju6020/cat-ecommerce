using System.Net;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// Extended by HO-ACC-001 §8.11 (US-ACC-12): <see cref="Category"/> and <see cref="IpAddress"/>
/// are required positional parameters with no defaults, so the compiler lists every writer that
/// has not yet decided them. <see cref="IpAddress"/> is null only when there is no HTTP request --
/// never a fabricated <c>0.0.0.0</c>.
/// </summary>
public sealed record AuditEntryDto(
    string EntityType,
    Guid EntityId,
    string Action,
    string Reason,
    Guid ActorUserId,
    DateTimeOffset OccurredOn,
    string? BeforeValue,
    string? AfterValue,
    UserContext UserContext,
    Guid CorrelationId,
    AuditCategory Category,
    IPAddress? IpAddress);

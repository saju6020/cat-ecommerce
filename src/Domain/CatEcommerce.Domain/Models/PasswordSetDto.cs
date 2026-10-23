using CatEcommerce.Domain.ValueObjects;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <param name="SetByAdministratorId">Owner, 2026-10-19: the administrator who set this password for someone else (null when
/// the holder set it themselves, e.g. through the reset link). Audited as the actor (SECURITY gate HIGH).</param>
public sealed record PasswordSetDto(PasswordCredential Credential, UserContext UserContext, Guid CorrelationId, Guid? SetByAdministratorId = null);

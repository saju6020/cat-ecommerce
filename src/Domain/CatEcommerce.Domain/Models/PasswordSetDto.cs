using CatEcommerce.Domain.ValueObjects;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record PasswordSetDto(PasswordCredential Credential, UserContext UserContext, Guid CorrelationId);

using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-ACC-001 §8.10a. Carries a timestamp and the platform context -- nothing personal.</summary>
public sealed record CustomerAnonymisationDto(DateTimeOffset AnonymisedOn, UserContext UserContext, Guid CorrelationId);

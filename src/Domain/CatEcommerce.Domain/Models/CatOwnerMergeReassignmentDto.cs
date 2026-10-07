using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-ACC-001 §8.9. The one Cat change this slice makes.</summary>
public sealed record CatOwnerMergeReassignmentDto(
    Guid DuplicateCustomerId,
    Guid SurvivingCustomerId,
    UserContext UserContext,
    Guid CorrelationId);

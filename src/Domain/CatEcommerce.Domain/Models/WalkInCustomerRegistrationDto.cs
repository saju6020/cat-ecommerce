using CatEcommerce.Domain.ValueObjects;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-ACC-001 §8.3 (RegisterWalkIn) / U-H3. No field can set a credential, an email or the
/// registration channel -- deliberately, so a walk-in cannot be created any other way than
/// incomplete.
/// </summary>
public sealed record WalkInCustomerRegistrationDto(
    string FullName,
    MobileNumber MobileNumber,
    UserContext UserContext,
    Guid CorrelationId);

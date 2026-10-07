using CatEcommerce.Domain.ValueObjects;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-ACC-001 §8.3 (UpdateContactDetails). No primary-mobile field exists here -- the mobile is
/// never editable from the profile (OQ-5).
/// </summary>
public sealed record CustomerContactDetailsDto(
    string FullName,
    EmailAddress? EmailAddress,
    IReadOnlyList<MobileNumber> AdditionalPhoneNumbers,
    UserContext UserContext,
    Guid CorrelationId);

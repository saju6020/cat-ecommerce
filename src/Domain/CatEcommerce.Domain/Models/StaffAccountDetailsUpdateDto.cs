using CatEcommerce.Domain.ValueObjects;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>Owner, 2026-10-20: an administrator corrects a staff member's name and sign-in identifiers.</summary>
public sealed record StaffAccountDetailsUpdateDto(
    string FullName,
    MobileNumber? MobileNumber,
    EmailAddress? EmailAddress,
    UserContext UserContext,
    Guid CorrelationId);

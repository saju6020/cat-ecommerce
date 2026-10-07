using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-SHP-001 section 8.6 / section 11.1 C9.</summary>
public sealed record SourcingOriginCreateDto(string CountryCode, string? SourcingNotes, bool IsFeatured, int SortOrder, UserContext UserContext, Guid CorrelationId);

public sealed record SourcingOriginUpdateDto(string? SourcingNotes, bool IsFeatured, int SortOrder, UserContext UserContext, Guid CorrelationId);

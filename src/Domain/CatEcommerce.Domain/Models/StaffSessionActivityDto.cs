namespace CatEcommerce.Domain.Models;

/// <summary>HO-ACC-001 §8.4 (RecordActivity). Raises no event -- a heartbeat is not an audited change.</summary>
public sealed record StaffSessionActivityDto(DateTimeOffset AsOf);

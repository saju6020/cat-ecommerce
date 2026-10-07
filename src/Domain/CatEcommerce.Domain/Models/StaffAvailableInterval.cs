namespace CatEcommerce.Domain.Models;

/// <summary>HO-ADM-001 §8.5. Result of <c>StaffAvailability.Intervals</c>. Never zero-length.</summary>
public sealed record StaffAvailableInterval(
    Guid StaffAccountId, Guid BranchId, DateTimeOffset Start, DateTimeOffset End,
    Guid? DefaultRoomId, bool OutsideOpeningHours);

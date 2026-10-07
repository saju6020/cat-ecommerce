namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-APT-001 §11.6 -- HO-ACC-001 <c>CustomerMergedEvent</c>'s reassignment contract for
/// waitlist entries, the same "find every matching row" gap <see cref="IFlaggableAppointmentFinder"/>
/// exists for.</summary>
public interface IWaitlistEntryFinder
{
    Task<IReadOnlyList<Guid>> FindByCustomerAsync(Guid customerId, CancellationToken ct = default);
}

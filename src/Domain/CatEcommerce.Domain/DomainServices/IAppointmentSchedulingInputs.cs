using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-APT-001 §8.12 -- Infrastructure port. Loads, for one branch and local date: the service, the
/// performers with their roles and HO-ADM-001 availability, the rooms, and the busy intervals
/// (non-stale -- a <c>Held</c> row whose <c>HoldExpiresOn</c> has passed is never returned as busy).
/// One place, used by <c>GetAvailableSlotsQuery</c> and by every command that takes time
/// (<c>SlotClaim</c>).
/// </summary>
public interface IAppointmentSchedulingInputs
{
    Task<AppointmentSchedulingInputSet> LoadAsync(AppointmentSchedulingInputRequest request, CancellationToken ct = default);
}

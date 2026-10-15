using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SRG-001 §8.5/§12.2 -- Infrastructure port. Loads, for one branch and local date: the service, the named surgeon and assistant with their HO-ADM-001 availability
/// (staff mode, so a shift outside opening hours counts), the named theatre, and the busy intervals of each of the surgeon, the assistant, the theatre and the cat --
/// occupying rows only, <b>excluding lapsed holds</b> and the surgery's own appointment on reschedule. One place, used by <c>GetSurgeryAvailabilityQuery</c> and by
/// the booking and reschedule handlers (<c>SurgerySlotClaim</c>). It reuses <see cref="IAppointmentSchedulingInputs"/> for rosters, leave and closures.
/// </summary>
public interface ISurgerySchedulingInputs
{
    Task<SurgeryInputSet> LoadAsync(SurgeryInputRequest request, CancellationToken ct = default);
}

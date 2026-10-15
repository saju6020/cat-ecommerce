using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-EMR-001 §8.13 -- the action list a visit read returns, computed from the <i>same</i>
/// <see cref="ActionRoles"/> table the endpoints enforce and from the visit's own status, so the
/// client renders only what the server would accept. The server still decides: this list is a hint,
/// never the authority (every endpoint is separately guarded).
/// <c>recordTreatment</c> (Phase F) is held with <c>Visits.WriteNote</c>, like the other vet-only clinical writes.
/// </summary>
public static class VisitAllowedActions
{
    public const string RecordVitals = "recordVitals";
    public const string EditNote = "editNote";
    public const string RecordTreatment = "recordTreatment";
    public const string OrderDiagnostic = "orderDiagnostic";
    public const string AttachPhoto = "attachPhoto";
    public const string SetFollowUp = "setFollowUp";
    public const string Finalise = "finalise";
    public const string AddAddendum = "addAddendum";

    public static IReadOnlyList<string> For(VisitStatus visitStatus, IReadOnlyList<string> callerRoles, AppointmentStatus appointmentStatus)
    {
        var actions = new List<string>();

        // A visit whose appointment was cancelled or recorded as a no-show cannot be worked on.
        var appointmentLive = appointmentStatus != AppointmentStatus.Cancelled && appointmentStatus != AppointmentStatus.NoShow;

        if (visitStatus == VisitStatus.Open && appointmentLive)
        {
            if (Holds(Actions.Visits.RecordVitals, callerRoles)) actions.Add(RecordVitals);
            if (Holds(Actions.Visits.WriteNote, callerRoles))
            {
                actions.Add(EditNote);
                actions.Add(RecordTreatment);
                actions.Add(OrderDiagnostic);
                actions.Add(AttachPhoto);
                actions.Add(SetFollowUp);
            }

            if (Holds(Actions.Visits.Finalise, callerRoles)) actions.Add(Finalise);
        }
        else if (visitStatus == VisitStatus.Finalised)
        {
            if (Holds(Actions.Visits.AddAddendum, callerRoles)) actions.Add(AddAddendum);
        }

        return actions;
    }

    private static bool Holds(string action, IReadOnlyList<string> callerRoles) =>
        ActionRoles.TryGetRoles(action, out var allowed) && allowed.Intersect(callerRoles, StringComparer.Ordinal).Any();
}

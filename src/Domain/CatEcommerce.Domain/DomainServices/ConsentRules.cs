using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-EMR-001 §8.9 -- <b>HO-SRG-001's contract</b> for US-CLN-21 AC2 / US-SRG-03: a consent is valid for
/// a procedure when it is <see cref="ConsentStatus.Recorded"/> (not withdrawn) and names the same cat,
/// the same appointment and the same kind. Pure: Surgery loads the consents and calls this; whether a
/// vet may override an absent consent is Surgery's decision, not this rule's.
/// </summary>
public static class ConsentRules
{
    public static bool IsValidFor(ConsentRecordAggregate consent, Guid catId, Guid appointmentId, ConsentKind kind) =>
        IsValidFor(consent.Status, consent.CatId, consent.AppointmentId, consent.Kind, catId, appointmentId, kind);

    /// <summary>The same rule over plain values, so a read path can apply it to projected columns without rehydrating the aggregate (ADR-012; HO-SRG-001 backlog row 35).</summary>
    public static bool IsValidFor(ConsentStatus status, Guid consentCatId, Guid consentAppointmentId, ConsentKind consentKind, Guid catId, Guid appointmentId, ConsentKind kind) =>
        status == ConsentStatus.Recorded
        && consentCatId == catId
        && consentAppointmentId == appointmentId
        && consentKind == kind;
}

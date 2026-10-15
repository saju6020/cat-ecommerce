using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SRG-001 §8.4 (ADR-158) -- adds a NEW surgery appointment to the scoped context WITHOUT saving, so the surgery repository's own save commits the surgery and its
/// appointment together (F23): the one recorded rule-1 exception of the scheduling kind. Exactly <c>EfNotificationOutbox</c>'s shape. An existing appointment needs no
/// enlisting -- the handler loads it through its repository, mutates it, and the surgery's save commits both.
/// </summary>
public interface ISurgeryAppointmentEnlister
{
    void Enlist(AppointmentAggregate appointment);
}

/// <summary>
/// HO-SRG-001 -- Surgery's own branch-local "today" (rule: a context that needs a branch-local date owns a clock port, the pattern of <see cref="ICatRecordClock"/> and
/// <see cref="IPharmacyClock"/>; it never reaches into Inventory's clock or Billing's business date). Zone conversion stays in HO-ADM-001's <c>BranchCalendar</c> (ADR-045).
/// </summary>
public interface ISurgeryClock
{
    Task<DateOnly> LocalTodayAsync(Guid branchId, CancellationToken ct);

    Task<DateOnly> LocalDateOfAsync(Guid branchId, DateTimeOffset instant, CancellationToken ct);

    /// <summary>The branch's IANA zone id (falls back to the main-branch default when the branch is gone).</summary>
    Task<string> TimeZoneIdAsync(Guid branchId, CancellationToken ct);
}

/// <summary>Surgery ids for a handler that must walk several rows (event handlers: weight, consent, merge, transfer). Read-only, <c>AsNoTracking</c>.</summary>
public interface ISurgeryFinder
{
    /// <summary>Surgeries of the cat that are still Scheduled or PreOp (the checklist is open).</summary>
    Task<IReadOnlyList<Guid>> FindChecklistOpenByCatAsync(Guid catId, CancellationToken ct = default);

    Task<Guid?> FindByAppointmentAsync(Guid appointmentId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> FindByCustomerAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Future Scheduled surgeries of a cat (ownership transfer).</summary>
    Task<IReadOnlyList<Guid>> FindFutureScheduledByCatAsync(Guid catId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>The number of non-cancelled surgeries booked under a campaign (the campaign's cap and its price lock).</summary>
    Task<int> CountForCampaignAsync(Guid campaignId, CancellationToken ct = default);
}

/// <summary>
/// HO-SRG-001 §8.7 (ADR-161) -- whether a cat's consent for a surgery is on record, RE-READ from the consent records each time (never from the checklist flag).
/// HO-EMR-001 publishes consent as <c>ConsentRules.IsValidFor</c> over a cat, the procedure-booking appointment and a kind (HO-EMR-001 §8.9); this port applies it for
/// the kinds a surgery requires (<c>Surgery</c> and <c>Anaesthesia</c>). Read-only.
/// </summary>
public interface ISurgeryConsentReader
{
    Task<SurgeryConsentCoverage> GetCoverageAsync(Guid catId, Guid appointmentId, CancellationToken ct = default);
}

/// <param name="Covered">Every required kind has a valid, non-withdrawn consent.</param>
/// <param name="ConsentId">The most recent valid consent that contributed (the checklist item's reference), when any.</param>
public sealed record SurgeryConsentCoverage(bool Covered, Guid? ConsentId);

/// <summary>
/// HO-SRG-001 §8.11 (ADR-171) -- the price a surgery is booked at. No campaign asked for: the standard price at now. A campaign asked for: it applies only when it is active,
/// the surgery's local date is within its dates, the branch is covered, the service has a campaign price, its case cap is not reached and an eligibility note is given;
/// each failure is its own <c>CampaignNotApplicable</c> reason. Read-only.
/// </summary>
public interface ISurgeryCampaignPricing
{
    Task<SurgeryPricingResult> ResolveAsync(SurgeryPricingRequest request, CancellationToken ct = default);
}

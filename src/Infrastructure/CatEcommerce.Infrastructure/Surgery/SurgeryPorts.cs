using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Surgery;

/// <summary>HO-SRG-001 §8.4 (ADR-158) -- adds a new surgery appointment to the scoped context WITHOUT saving; the surgery's repository save commits both (F23).</summary>
public sealed class SurgeryAppointmentEnlister(CatEcommerceDbContext db) : ISurgeryAppointmentEnlister
{
    public void Enlist(AppointmentAggregate appointment) => db.Set<AppointmentAggregate>().Add(appointment);
}

/// <summary>HO-SRG-001 -- Surgery's own branch-local "today" (the pattern of <c>CatRecordClock</c> / <c>PharmacyClock</c>). Zone conversion stays in <c>BranchCalendar</c> (ADR-045).</summary>
public sealed class SurgeryClock(CatEcommerceDbContext db, TimeProvider timeProvider) : ISurgeryClock
{
    private const string FallbackZone = "Asia/Dhaka";

    public async Task<DateOnly> LocalTodayAsync(Guid branchId, CancellationToken ct) =>
        BranchCalendar.LocalDateOf(timeProvider.GetUtcNow(), await TimeZoneIdAsync(branchId, ct));

    public async Task<DateOnly> LocalDateOfAsync(Guid branchId, DateTimeOffset instant, CancellationToken ct) =>
        BranchCalendar.LocalDateOf(instant, await TimeZoneIdAsync(branchId, ct));

    public async Task<string> TimeZoneIdAsync(Guid branchId, CancellationToken ct) =>
        await db.Branches.AsNoTracking().Where(b => b.Id == branchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync(ct) ?? FallbackZone;
}

/// <summary>HO-SRG-001 -- id lookups for the event handlers. <c>AsNoTracking</c>; the handler loads each row through its repository.</summary>
public sealed class SurgeryFinder(CatEcommerceDbContext db) : ISurgeryFinder
{
    public async Task<IReadOnlyList<Guid>> FindChecklistOpenByCatAsync(Guid catId, CancellationToken ct = default) =>
        await db.Surgeries.AsNoTracking()
            .Where(s => !s.IsMarkedToDelete && s.CatId == catId && (s.Status == SurgeryStatus.Scheduled || s.Status == SurgeryStatus.PreOp))
            .Select(s => s.Id).ToListAsync(ct);

    public async Task<Guid?> FindByAppointmentAsync(Guid appointmentId, CancellationToken ct = default) =>
        await db.Surgeries.AsNoTracking().Where(s => !s.IsMarkedToDelete && s.AppointmentId == appointmentId).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await db.Surgeries.AsNoTracking().Where(s => !s.IsMarkedToDelete && s.CustomerId == customerId).Select(s => s.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindFutureScheduledByCatAsync(Guid catId, DateTimeOffset now, CancellationToken ct = default) =>
        await db.Surgeries.AsNoTracking()
            .Where(s => !s.IsMarkedToDelete && s.CatId == catId && s.Status == SurgeryStatus.Scheduled && s.StartsOn > now)
            .Select(s => s.Id).ToListAsync(ct);

    public async Task<int> CountForCampaignAsync(Guid campaignId, CancellationToken ct = default) =>
        await db.Surgeries.AsNoTracking()
            .CountAsync(s => !s.IsMarkedToDelete && s.PriceSnapshot.CampaignId == campaignId && s.Status != SurgeryStatus.Cancelled, ct);
}

/// <summary>
/// HO-SRG-001 §8.7 (ADR-161) -- consent, RE-READ from HO-EMR-001's consent records each time. EMR publishes the rule (<see cref="ConsentRules.IsValidFor"/>: recorded, same
/// cat, same procedure-booking appointment, same kind); this applies it for the two kinds a surgery requires. A withdrawn consent never counts.
/// </summary>
public sealed class SurgeryConsentReader(CatEcommerceDbContext db) : ISurgeryConsentReader
{
    private static readonly ConsentKind[] RequiredKinds = [ConsentKind.Surgery, ConsentKind.Anaesthesia];

    public async Task<SurgeryConsentCoverage> GetCoverageAsync(Guid catId, Guid appointmentId, CancellationToken ct = default)
    {
        var consents = await db.Set<ConsentRecordAggregate>().AsNoTracking()
            .Where(c => !c.IsMarkedToDelete && c.CatId == catId && c.AppointmentId == appointmentId && c.Status == ConsentStatus.Recorded)
            .Select(c => new { c.Id, c.Status, c.CatId, c.AppointmentId, c.Kind, c.SignedOn })
            .ToListAsync(ct);

        var covering = RequiredKinds
            .Select(kind => consents.Where(c => ConsentRules.IsValidFor(c.Status, c.CatId, c.AppointmentId, c.Kind, catId, appointmentId, kind)).OrderByDescending(c => c.SignedOn).FirstOrDefault())
            .ToList();

        return covering.All(c => c is not null)
            ? new SurgeryConsentCoverage(true, covering.Select(c => c!).OrderByDescending(c => c.SignedOn).First().Id)
            : new SurgeryConsentCoverage(false, null);
    }
}

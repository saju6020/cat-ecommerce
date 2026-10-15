using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Prescription;

internal static class CustomerPrescriptionReads
{
    /// <summary>The repeat block, built from the SAME rule function <c>RequestRepeat</c> applies, so <c>canRequest</c> is never more generous than the command.</summary>
    public static CustomerRepeatViewModel Repeat(PrescriptionRow row, RepeatRequestViewModel? open, DateOnly today)
    {
        var suppliesLeft = PrescriptionActivityRules.SuppliesLeft(row.RepeatsAuthorised, row.SuppliesDispensed);
        var eligibility = PrescriptionActionRules.EvaluateRepeat(row.IsRepeatable, row.Status, suppliesLeft, row.ValidUntil, open is not null, today);
        return new CustomerRepeatViewModel(row.IsRepeatable, Math.Max(0, suppliesLeft), row.ValidUntil, eligibility.CanRequest, eligibility.Message, eligibility.ViolationCode, open);
    }

    public static async Task<Dictionary<Guid, RepeatRequestViewModel>> OpenRequestsAsync(CatEcommerceDbContext db, IReadOnlyCollection<Guid> prescriptionIds, CancellationToken ct)
    {
        if (prescriptionIds.Count == 0) return [];
        var ids = prescriptionIds.ToList();

        var rows = await db.Prescriptions.AsNoTracking().Where(p => ids.Contains(p.Id))
            .SelectMany(p => p.RepeatRequests.Where(r => r.Status == RepeatRequestStatus.Requested)
                .Select(r => new { PrescriptionId = p.Id, r.Id, r.RequestedOn, r.PreferredBranchId, r.Note, r.Status, r.DecidedOn, r.DeclineReason, r.DispensingId }))
            .ToListAsync(ct);

        return rows.GroupBy(r => r.PrescriptionId).ToDictionary(
            g => g.Key,
            g =>
            {
                var r = g.OrderByDescending(x => x.RequestedOn).First();
                return new RepeatRequestViewModel(r.Id, r.RequestedOn, r.PreferredBranchId, r.Note, r.Status.DisplayName, r.DecidedOn, r.DeclineReason, r.DispensingId);
            });
    }
}

/// <summary>US-PHM-07 -- the owner's prescriptions per cat: medicine, dose, frequency, remaining duration and issuing vet; completed-but-still-running included under
/// <c>active</c>, cancelled excluded. Only the caller's CURRENT cats (after an ownership transfer the previous owner sees nothing).</summary>
public sealed class GetCustomerPrescriptionsQueryHandler(CatEcommerceDbContext db, IPharmacyClock clock)
    : IQueryHandlerAsync<GetCustomerPrescriptionsQuery, IReadOnlyList<CustomerPrescriptionViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<CustomerPrescriptionViewModel>>> HandleAsync(GetCustomerPrescriptionsQuery query)
    {
        var ct = CancellationToken.None;
        var today = await clock.MainBranchTodayAsync(ct);
        var customerId = query.CustomerId;

        var prescriptions = db.Prescriptions.Where(p => !p.IsMarkedToDelete && db.Cats.Any(c => c.Id == p.CatId && c.OwnerId == customerId));
        if (query.CatId is { } catId) prescriptions = prescriptions.Where(p => p.CatId == catId);

        var rows = await PrescriptionReads.Rows(db, prescriptions).ToListAsync(ct);
        var wantActive = !string.Equals(query.Scope, "past", StringComparison.OrdinalIgnoreCase);
        var chosen = rows.Where(r => PrescriptionReads.IsActive(r, today) == wantActive).OrderByDescending(r => r.IssuedOn).ThenBy(r => r.Id).Take(200).ToList();

        var catNames = await ControlledDrugReads.CatNamesAsync(db, chosen.Select(r => (Guid?)r.CatId), ct);
        var open = await CustomerPrescriptionReads.OpenRequestsAsync(db, chosen.Select(r => r.Id).ToList(), ct);

        IReadOnlyList<CustomerPrescriptionViewModel> result = chosen.Select(r => ToView(r, catNames, open.GetValueOrDefault(r.Id), today)).ToList();
        return new QueryResponse<IReadOnlyList<CustomerPrescriptionViewModel>> { Result = result };
    }

    internal static CustomerPrescriptionViewModel ToView(PrescriptionRow r, IReadOnlyDictionary<Guid, string> catNames, RepeatRequestViewModel? open, DateOnly today) => new(
        r.Id, r.CatId, catNames.GetValueOrDefault(r.CatId) ?? "", new PrescriptionMedicineViewModel(r.MedicineName, r.MedicineStrengthText, r.MedicineForm.DisplayName, false),
        QuantityViewModel.Of(r.DoseAmount, r.DoseUnit.DisplayName), r.Route.DisplayName, r.Frequency.Label,
        PrescriptionActivityRules.RemainingDays(r.StartsOn, r.LastDispensedOn, r.DurationDays, today),
        PrescriptionActivityRules.CourseEndsOn(r.StartsOn, r.LastDispensedOn, r.DurationDays), r.PrescriberName, r.IssuedOn,
        PrescriptionReads.EffectiveStatus(r, today).DisplayName, PrescriptionReads.IsActive(r, today), CustomerPrescriptionReads.Repeat(r, open, today));
}

public sealed class GetCustomerPrescriptionDetailQueryHandler(CatEcommerceDbContext db, IPharmacyClock clock, INotificationLinkBuilder links)
    : IQueryHandlerAsync<GetCustomerPrescriptionDetailQuery, CustomerPrescriptionDetailViewModel?>
{
    public async Task<QueryResponse<CustomerPrescriptionDetailViewModel?>> HandleAsync(GetCustomerPrescriptionDetailQuery query)
    {
        var ct = CancellationToken.None;
        var customerId = query.CustomerId;
        var today = await clock.MainBranchTodayAsync(ct);

        var row = await PrescriptionReads.Rows(db, db.Prescriptions.Where(p =>
                p.Id == query.PrescriptionId && !p.IsMarkedToDelete && db.Cats.Any(c => c.Id == p.CatId && c.OwnerId == customerId)))
            .FirstOrDefaultAsync(ct);
        if (row is null) return new QueryResponse<CustomerPrescriptionDetailViewModel?> { Result = null };

        var instructions = await db.Prescriptions.AsNoTracking().Where(p => p.Id == row.Id).Select(p => p.Instructions).FirstAsync(ct);
        var catNames = await ControlledDrugReads.CatNamesAsync(db, [row.CatId], ct);
        var open = (await CustomerPrescriptionReads.OpenRequestsAsync(db, [row.Id], ct)).GetValueOrDefault(row.Id);

        var supplies = await db.Dispensings.AsNoTracking().Where(d => d.PrescriptionId == row.Id && d.Status == DispensingStatus.Dispensed)
            .Select(d => new { d.DispensedOnLocal, d.BranchId, d.SupplyNumber, Quantity = d.Lines.Sum(l => l.Quantity) })
            .ToListAsync(ct);
        var branchIds = supplies.Select(s => s.BranchId).Distinct().ToList();
        var branches = branchIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await db.Branches.AsNoTracking().Where(b => branchIds.Contains(b.Id)).Select(b => new { b.Id, b.Name }).ToListAsync(ct)).ToDictionary(b => b.Id, b => b.Name);
        var unit = row.DispensingUnit.DisplayName;

        var summary = GetCustomerPrescriptionsQueryHandler.ToView(row, catNames, open, today);
        var detail = new CustomerPrescriptionDetailViewModel(
            summary.Id, summary.CatId, summary.CatName, summary.Medicine, summary.Dose, summary.Route, summary.Frequency, summary.RemainingDays, summary.CourseEndsOn,
            summary.IssuingVetName, summary.IssuedOn, summary.Status, summary.IsActive, summary.Repeat, instructions,
            supplies.OrderBy(s => s.SupplyNumber).Select(s => new CustomerDispensingViewModel(s.DispensedOnLocal, branches.GetValueOrDefault(s.BranchId) ?? "", QuantityViewModel.Of(s.Quantity, unit))).ToList(),
            links.CatCareLink(row.CatId, "FollowUp"));

        return new QueryResponse<CustomerPrescriptionDetailViewModel?> { Result = detail };
    }
}

public sealed class GetExpirablePrescriptionsQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetExpirablePrescriptionsQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetExpirablePrescriptionsQuery query)
    {
        // A superset of what any branch's local date calls expired: UTC today is never ahead of a local date that has not yet passed ValidUntil.
        var utcToday = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        IReadOnlyList<Guid> ids = await db.Prescriptions.AsNoTracking()
            .Where(p => !p.IsMarkedToDelete && (p.Status == PrescriptionStatus.Issued || p.Status == PrescriptionStatus.Dispensed) && p.ValidUntil <= utcToday
                        && p.SuppliesDispensed < 1 + p.RepeatsAuthorised)
            .OrderBy(p => p.ValidUntil).Select(p => p.Id).Take(query.Limit).ToListAsync();

        return new QueryResponse<IReadOnlyList<Guid>> { Result = ids };
    }
}

/// <summary>§8.11 -- the sweep's input: repeat prescriptions in their reminder window, to the cat's CURRENT owner, with no repeat request already open. The window itself is the pure
/// <see cref="RefillScheduleCalculator"/> -- the same function the send-time guard re-applies.</summary>
public sealed class GetDueRefillRemindersQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetDueRefillRemindersQuery, IReadOnlyList<DueRefillReminderViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<DueRefillReminderViewModel>>> HandleAsync(GetDueRefillRemindersQuery query)
    {
        var rows = await (
            from p in db.Prescriptions.AsNoTracking()
            join c in db.Cats.AsNoTracking() on p.CatId equals c.Id
            where !p.IsMarkedToDelete && p.IsRepeatable && p.Status == PrescriptionStatus.Dispensed && p.LastDispensedOn != null
                  && p.SuppliesDispensed < 1 + p.RepeatsAuthorised && c.LifeStatus == CatLifeStatus.Alive
                  && !p.RepeatRequests.Any(r => r.Status == RepeatRequestStatus.Requested)
            select new
            {
                p.Id, p.CatId, CustomerId = c.OwnerId, CatName = c.Name, MedicineName = p.MedicineSnapshot.Name, p.BranchId, p.Status, p.LastDispensedOn, p.DurationDays,
                p.ValidUntil, p.RepeatsAuthorised, p.SuppliesDispensed
            }).ToListAsync();

        IReadOnlyList<DueRefillReminderViewModel> due = rows
            .Select(r => new
            {
                Row = r,
                Schedule = RefillScheduleCalculator.Due(
                    true, r.Status, PrescriptionActivityRules.SuppliesLeft(r.RepeatsAuthorised, r.SuppliesDispensed), r.LastDispensedOn, r.DurationDays, r.ValidUntil, query.Today, query.LeadDays)
            })
            .Where(x => x.Schedule is not null)
            .Select(x => new DueRefillReminderViewModel(
                x.Row.Id, x.Row.CustomerId, x.Row.CatId, x.Row.CatName, x.Row.MedicineName, x.Row.BranchId, x.Schedule!.RefillDueOn, x.Row.ValidUntil, x.Row.SuppliesDispensed))
            .ToList();

        return new QueryResponse<IReadOnlyList<DueRefillReminderViewModel>> { Result = due };
    }
}

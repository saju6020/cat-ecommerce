using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Prescription;

/// <summary>
/// HO-PHM-001 §12.4 / §8.7 (US-PHM-05) -- the server's FEFO plan for a prescription at the caller's branch. It asks Inventory (the published <see cref="IStockLedger.GetDispensaryBatchesAsync"/>)
/// which batches the clinic dispensary holds, runs the pure <see cref="DispensingBatchPlanner"/> and returns every batch with its expired / dispensable flags, the suggested
/// lines and whether only expired stock is left. <b>It writes nothing and adds nothing the planner did not decide.</b>
/// </summary>
public sealed class GetDispensePlanQueryHandler(
    CatEcommerceDbContext db, IStockLedger ledger, ICatalogueReader catalogue, IPharmacyClock clock)
    : IQueryHandlerAsync<GetDispensePlanQuery, DispensePlanViewModel?>
{
    public async Task<QueryResponse<DispensePlanViewModel?>> HandleAsync(GetDispensePlanQuery query)
    {
        var ct = CancellationToken.None;
        var today = await clock.LocalTodayAsync(query.BranchId, ct);

        var row = await PrescriptionReads.Rows(db, db.Prescriptions.Where(p => p.Id == query.PrescriptionId && !p.IsMarkedToDelete)).FirstOrDefaultAsync(ct);
        if (row is null) return new QueryResponse<DispensePlanViewModel?> { Result = null };

        var facts = await db.Prescriptions.AsNoTracking().Where(p => p.Id == query.PrescriptionId)
            .Select(p => new { p.MedicineSnapshot.LinkedSkuIds, p.Quantity }).FirstAsync(ct);
        var cat = await db.Cats.AsNoTracking().Where(c => c.Id == row.CatId).Select(c => new { c.Id, c.Name, c.IsHighRisk, c.HandlingNote, c.LifeStatus }).FirstAsync(ct);

        var held = await ledger.GetDispensaryBatchesAsync(query.BranchId, facts.LinkedSkuIds, ct);
        var offers = BatchOfferFactory.FromCandidates(facts.LinkedSkuIds, held.IsSuccess ? held.Value! : []);
        var plan = DispensingBatchPlanner.Plan(facts.Quantity, offers, today);
        var skus = (await catalogue.GetSkusAsync(facts.LinkedSkuIds, ct)).ToDictionary(s => s.SkuId);
        var unit = row.DispensingUnit.DisplayName;

        var summary = PrescriptionReads.ToSummary(row, today);
        var suppliesLeft = PrescriptionActivityRules.SuppliesLeft(row.RepeatsAuthorised, row.SuppliesDispensed);

        string? blocking = null;
        if (row.Status == PrescriptionStatus.Cancelled) blocking = "This prescription was cancelled.";
        else if (suppliesLeft <= 0) blocking = "Every supply of this prescription has been dispensed.";
        else if (today > row.ValidUntil) blocking = PharmacyMessages.PrescriptionValidityEnded(row.ValidUntil);
        else if (cat.LifeStatus != CatLifeStatus.Alive) blocking = "This cat can no longer be treated.";
        else if (facts.LinkedSkuIds.Length == 0) blocking = $"{row.MedicineName} has no stocked SKU.";
        else if (plan.OnlyExpiredStockAvailable)
            blocking = PharmacyMessages.OnlyExpiredStockAvailable(row.MedicineName, plan.ExpiredExample!.LotNumber, plan.ExpiredExample.ExpiresOn!.Value);
        else if (plan.TotalDispensable < facts.Quantity) blocking = PharmacyMessages.InsufficientDispensaryStock(plan.TotalDispensable);

        var batches = offers
            .OrderBy(o => o.ExpiresOn is null).ThenBy(o => o.ExpiresOn).ThenBy(o => o.LotNumber, StringComparer.Ordinal)
            .Select(o =>
            {
                var sku = skus.GetValueOrDefault(o.SkuId);
                var expired = DispensingBatchPlanner.IsExpired(o, today);
                return new DispensePlanBatchViewModel(
                    o.SkuId, sku?.ProductName ?? "", sku?.VariantLabel ?? "", o.BatchId, o.LotNumber, sku?.Barcode, o.ExpiresOn, QuantityViewModel.Of(o.Available, unit), expired,
                    o.IsDispensable && !expired, expired ? PharmacyMessages.BatchExpired(o.LotNumber, o.ExpiresOn!.Value) : o.NotDispensableReason);
            }).ToList();

        var plan2 = new DispensePlanViewModel(
            summary, new PlanCatViewModel(cat.Id, cat.Name, cat.IsHighRisk, cat.HandlingNote), QuantityViewModel.Of(facts.Quantity, unit), row.IsControlled,
            ControlledDrugRules.WitnessRoles, blocking is null, blocking, batches,
            plan.SuggestedLines.Select(l => new SuggestedLineViewModel(l.SkuId, l.BatchId, QuantityViewModel.Of(l.Quantity, unit))).ToList(),
            plan.OnlyExpiredStockAvailable, plan.Shortfall > 0m ? QuantityViewModel.Of(plan.Shortfall, unit) : null);

        return new QueryResponse<DispensePlanViewModel?> { Result = plan2 };
    }
}

/// <summary>The pharmacy queue of the active branch: prescriptions issued here still awaiting their first supply, and the owners' repeat requests that chose this branch. Only
/// what can still be dispensed today is shown.</summary>
public sealed class GetPharmacyQueueQueryHandler(CatEcommerceDbContext db, IPharmacyClock clock, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetPharmacyQueueQuery, PharmacyQueueViewModel>
{
    public async Task<QueryResponse<PharmacyQueueViewModel>> HandleAsync(GetPharmacyQueueQuery query)
    {
        var ct = CancellationToken.None;
        var today = await clock.LocalTodayAsync(query.BranchId, ct);

        var waiting = await PrescriptionReads.Rows(db, db.Prescriptions.Where(p =>
                p.BranchId == query.BranchId && p.Status == PrescriptionStatus.Issued && p.SuppliesDispensed == 0 && p.ValidUntil >= today && !p.IsMarkedToDelete))
            .OrderBy(r => r.IssuedOn).Take(200).ToListAsync(ct);

        var repeats = await db.Prescriptions.AsNoTracking()
            .Where(p => p.Status == PrescriptionStatus.Dispensed && p.ValidUntil >= today && !p.IsMarkedToDelete)
            .SelectMany(p => p.RepeatRequests.Where(r => r.Status == RepeatRequestStatus.Requested && r.PreferredBranchId == query.BranchId).Select(r => new
            {
                RequestId = r.Id, PrescriptionId = p.Id, p.PrescriptionNumber, p.CatId, p.CustomerId, p.MedicineSnapshot.Name, p.MedicineSnapshot.StrengthText,
                p.MedicineSnapshot.Form, p.MedicineSnapshot.IsControlled, r.RequestedOn, r.Note
            }))
            .OrderBy(r => r.RequestedOn).Take(200).ToListAsync(ct);

        var catIds = waiting.Select(w => (Guid?)w.CatId).Concat(repeats.Select(r => (Guid?)r.CatId)).Distinct().ToList();
        var customerIds = waiting.Select(w => (Guid?)w.CustomerId).Concat(repeats.Select(r => (Guid?)r.CustomerId)).Distinct().ToList();
        var cats = catIds.Count == 0 ? [] : await db.Cats.AsNoTracking().Where(c => catIds.Contains(c.Id)).Select(c => new { c.Id, c.Name, c.IsHighRisk }).ToListAsync(ct);
        var customers = await ControlledDrugReads.CustomerNamesAsync(db, customerIds, ct);
        var catById = cats.ToDictionary(c => c.Id);

        var items = waiting.Select(w => new PharmacyQueueItemViewModel(
            PrescriptionReads.ToSummary(w, today), catById.GetValueOrDefault(w.CatId)?.Name ?? "", catById.GetValueOrDefault(w.CatId)?.IsHighRisk ?? false,
            customers.GetValueOrDefault(w.CustomerId) ?? "")).ToList();

        var repeatItems = repeats.Select(r => new PharmacyQueueRepeatViewModel(
            r.RequestId, r.PrescriptionId, r.PrescriptionNumber, catById.GetValueOrDefault(r.CatId)?.Name ?? "", customers.GetValueOrDefault(r.CustomerId) ?? "",
            new PrescriptionMedicineViewModel(r.Name, r.StrengthText, r.Form.DisplayName, r.IsControlled), r.RequestedOn, r.Note)).ToList();

        return new QueryResponse<PharmacyQueueViewModel> { Result = new PharmacyQueueViewModel(timeProvider.GetUtcNow(), items, repeatItems) };
    }
}

public sealed class LookupPrescriptionByNumberQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<LookupPrescriptionByNumberQuery, PrescriptionLookupViewModel?>
{
    public async Task<QueryResponse<PrescriptionLookupViewModel?>> HandleAsync(LookupPrescriptionByNumberQuery query)
    {
        var number = query.Number.Trim().ToUpperInvariant();
        var id = await db.Prescriptions.AsNoTracking().Where(p => p.PrescriptionNumber == number && !p.IsMarkedToDelete).Select(p => (Guid?)p.Id).FirstOrDefaultAsync();

        return new QueryResponse<PrescriptionLookupViewModel?> { Result = id is null ? null : new PrescriptionLookupViewModel(id.Value) };
    }
}

/// <summary>A dispensing: its lines (lot and expiry, with the price snapshot's presence), the FROZEN label, the print count and what the caller may do with it now.</summary>
public sealed class GetDispensingQueryHandler(CatEcommerceDbContext db, IBusinessSettings settings, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetDispensingQuery, DispensingViewModel?>
{
    public async Task<QueryResponse<DispensingViewModel?>> HandleAsync(GetDispensingQuery query)
    {
        var ct = CancellationToken.None;
        var header = await db.Dispensings.AsNoTracking().Where(d => d.Id == query.DispensingId && d.BranchId == query.BranchId && !d.IsMarkedToDelete)
            .Select(d => new
            {
                d.Id, d.PrescriptionId, d.CatId, d.PrescriptionNumber, d.SupplyNumber, d.IsRepeat, d.Status, d.DispensedOn, d.DispensedOnLocal, d.DispensedBy, d.WitnessId,
                d.FefoOverrideReason, d.LabelPrintCount, d.ReversedOn, d.ReversalReason, d.BranchId,
                Label = new MedicineLabelViewModel(
                    d.Label.CatName, d.Label.OwnerName, d.Label.MedicineLine, d.Label.DoseLine, d.Label.Instructions, d.Label.DispensedOnLocal, d.Label.ExpiresOn,
                    d.Label.QuantityLine, d.Label.PrescriptionNumber, d.Label.PrescriberName, d.Label.ClinicName, d.Label.ClinicPhone, d.Label.ClinicAddress,
                    d.Label.ControlledNotice, d.Label.StandardWarnings)
            })
            .FirstOrDefaultAsync(ct);
        if (header is null) return new QueryResponse<DispensingViewModel?> { Result = null };

        var lines = await db.Dispensings.AsNoTracking().Where(d => d.Id == query.DispensingId)
            .SelectMany(d => d.Lines.Select(l => new
            {
                l.Id, l.SkuId, l.BatchId, l.LotNumber, l.ExpiresOn, l.Quantity,
                UnitPrice = l.PriceSnapshot == null ? (decimal?)null : l.PriceSnapshot.UnitPrice,
                Currency = l.PriceSnapshot == null ? null : l.PriceSnapshot.CurrencyCode
            }))
            .ToListAsync(ct);

        var unitOfMedicine = await db.Prescriptions.AsNoTracking().Where(p => p.Id == header.PrescriptionId).Select(p => p.MedicineSnapshot.DispensingUnit).FirstAsync(ct);
        var unit = unitOfMedicine.DisplayName;
        var names = await PrescriptionReads.StaffNamesAsync(db, [header.DispensedBy, header.WitnessId], ct);

        var now = timeProvider.GetUtcNow();
        var windowHours = settings.GetInt(SettingKeys.PharmacyDispensingReversalHours, header.BranchId);
        var roles = query.CallerRoles.ToHashSet(StringComparer.Ordinal);
        var allowed = new List<string>();
        if (header.Status == DispensingStatus.Dispensed)
        {
            if (roles.Overlaps([StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator])) allowed.Add("Reprint");

            var latest = await db.Prescriptions.AsNoTracking().Where(p => p.Id == header.PrescriptionId).Select(p => p.SuppliesDispensed).FirstAsync(ct);
            if (roles.Overlaps([StaffRoles.Vet, StaffRoles.Administrator]) && windowHours > 0 && now <= header.DispensedOn.AddHours(windowHours) && header.SupplyNumber == latest)
                allowed.Add("Reverse");
        }

        var view = new DispensingViewModel(
            header.Id, header.PrescriptionId, header.CatId, header.PrescriptionNumber, header.SupplyNumber, header.IsRepeat, header.Status.DisplayName, header.DispensedOn, header.DispensedOnLocal,
            PrescriptionReads.NameOf(names, header.DispensedBy), header.WitnessId is null ? null : PrescriptionReads.NameOf(names, header.WitnessId),
            header.FefoOverrideReason,
            lines.Select(l => new DispensingLineViewModel(
                l.Id, l.SkuId, l.BatchId, l.LotNumber, l.ExpiresOn, QuantityViewModel.Of(l.Quantity, unit),
                l.UnitPrice is { } price ? QuantityViewModel.Of(price, l.Currency ?? "") : null, l.Currency, l.UnitPrice is not null)).ToList(),
            header.Label, header.LabelPrintCount, header.ReversedOn, header.ReversalReason, allowed);

        return new QueryResponse<DispensingViewModel?> { Result = view };
    }
}

using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Billing;

/// <summary>HO-BIL-001 §12.6 -- explicit projection, <c>AsNoTracking</c>, paged in SQL.</summary>
public sealed class GetChargesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetChargesQuery, PagedResult<ChargeViewModel>>
{
    public async Task<QueryResponse<PagedResult<ChargeViewModel>>> HandleAsync(GetChargesQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);
        var pageNumber = Math.Max(query.PageNumber, 1);

        var charges = db.Set<ChargeAggregate>().AsNoTracking().Where(c => !c.IsMarkedToDelete && c.BranchId == query.BranchId);
        if (query.CustomerId is { } customerId) charges = charges.Where(c => c.CustomerId == customerId);
        if (query.BillingGroupId is { } groupId) charges = charges.Where(c => c.BillingGroupId == groupId);
        if (!string.IsNullOrWhiteSpace(query.SourceKind) && EnumerationParsing.TryParse<ChargeSourceKind>(query.SourceKind, out var kind) && kind is not null)
            charges = charges.Where(c => c.SourceKind == kind);

        charges = ChargeStatusFilter.Apply(db, charges, query.Status);

        var total = await charges.CountAsync();
        var rows = await (
            from c in charges.OrderByDescending(c => c.RecordedOn).ThenBy(c => c.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            join customer in db.Set<CustomerAccountAggregate>().AsNoTracking() on c.CustomerId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            select new
            {
                c.Id, c.CustomerId, CustomerName = customer != null ? customer.FullName : "", c.BranchId, c.CatId, c.BusinessLine, c.ItemKind,
                c.SourceKind, c.SourceId, c.SourceLineRef, c.BillingGroupKind, c.BillingGroupId, c.Description, c.Quantity, c.UnitPriceAmount,
                c.LineDiscountAmount, c.CurrencyCode, c.TaxClassId, c.ServiceDate, c.Status, c.RecordedOn, c.VoidReason,
            }).ToListAsync();

        var billed = await BilledOnInvoiceLookup.ForChargesAsync(db, rows.Select(r => r.Id).ToList());

        var items = rows.Select(r => new ChargeViewModel(
            r.Id, r.CustomerId, r.CustomerName, r.BranchId, r.CatId, r.BusinessLine.DisplayName, r.ItemKind.DisplayName,
            r.SourceKind.DisplayName, r.SourceId, r.SourceLineRef, r.BillingGroupKind?.DisplayName, r.BillingGroupId, r.Description,
            BillingMoney.Quantity(r.Quantity), BillingMoney.Of(r.UnitPriceAmount, r.CurrencyCode), BillingMoney.Of(r.LineDiscountAmount, r.CurrencyCode),
            BillingMoney.Of(decimal.Round(r.Quantity * r.UnitPriceAmount, 2, MidpointRounding.AwayFromZero) - r.LineDiscountAmount, r.CurrencyCode),
            r.TaxClassId, r.ServiceDate, r.Status.DisplayName, r.RecordedOn, r.VoidReason, billed.GetValueOrDefault(r.Id))).ToList();

        return new QueryResponse<PagedResult<ChargeViewModel>> { Result = new PagedResult<ChargeViewModel>(items, pageNumber, pageSize, total) };
    }
}

/// <summary>The status vocabulary of the charges list (§12.4): <c>unbilled</c>, <c>billed</c>, <c>voided</c>.</summary>
internal static class ChargeStatusFilter
{
    public static IQueryable<ChargeAggregate> Apply(CatEcommerceDbContext db, IQueryable<ChargeAggregate> charges, string? status) =>
        (status ?? string.Empty).ToLowerInvariant() switch
        {
            "voided" => charges.Where(c => c.Status == ChargeStatus.Voided),
            "unbilled" => charges.Where(c => c.Status == ChargeStatus.Active && !db.Set<InvoiceAggregate>().Any(i => i.Status != InvoiceStatus.Discarded && i.Lines.Any(l => l.ChargeId == c.Id))),
            "billed" => charges.Where(c => c.Status == ChargeStatus.Active && db.Set<InvoiceAggregate>().Any(i => i.Status != InvoiceStatus.Discarded && i.Lines.Any(l => l.ChargeId == c.Id))),
            _ => charges,
        };
}

/// <summary>"Billed" is derived (ADR-178): a line of a non-discarded invoice references the charge.</summary>
internal static class BilledOnInvoiceLookup
{
    public static async Task<Dictionary<Guid, BilledOnInvoiceViewModel>> ForChargesAsync(CatEcommerceDbContext db, IReadOnlyCollection<Guid> chargeIds)
    {
        if (chargeIds.Count == 0) return [];

        var ids = chargeIds.ToList();
        var rows = await db.Set<InvoiceAggregate>().AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Discarded)
            .SelectMany(i => i.Lines.Where(l => l.ChargeId != null && ids.Contains(l.ChargeId.Value))
                .Select(l => new { ChargeId = l.ChargeId!.Value, InvoiceId = i.Id, i.Number, i.Status }))
            .ToListAsync();

        return rows.ToDictionary(r => r.ChargeId, r => new BilledOnInvoiceViewModel(r.InvoiceId, r.Number, r.Status.DisplayName));
    }
}

public sealed class GetUncapturedSourcesQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetUncapturedSourcesQuery, IReadOnlyList<UncapturedSourceViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<UncapturedSourceViewModel>>> HandleAsync(GetUncapturedSourcesQuery query)
    {
        var timeZoneId = await db.Set<CatEcommerce.Domain.Aggregates.Administration.BranchAggregate>().AsNoTracking()
            .Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        var rangeFrom = new DateTimeOffset(query.From.ToDateTime(TimeOnly.MinValue), zone.GetUtcOffset(query.From.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();
        var rangeTo = new DateTimeOffset(query.To.AddDays(1).ToDateTime(TimeOnly.MinValue), zone.GetUtcOffset(query.To.AddDays(1).ToDateTime(TimeOnly.MinValue))).ToUniversalTime();

        var appointmentKind = ChargeSourceKind.Appointment;
        var rows = await (
            from a in db.Set<AppointmentAggregate>().AsNoTracking()
            where !a.IsMarkedToDelete && a.BranchId == query.BranchId && a.Status == AppointmentStatus.Completed && a.SurgeryId == null // a surgery's fee is the Surgery source's (Phase J gate)
                  && a.CompletedOn != null && a.CompletedOn >= rangeFrom && a.CompletedOn < rangeTo
            where !db.Set<ChargeAggregate>().Any(c => c.SourceKind == appointmentKind && c.SourceId == a.Id)
            orderby a.CompletedOn
            select new { a.Id, a.CustomerId, CompletedOn = a.CompletedOn!.Value, Label = a.ServiceSnapshot.Name }).ToListAsync();

        // Phase J. A multi-line source is "uncaptured" while it has FEWER recorded charges than billable lines (any status counts: a voided charge is still recorded and
        // is never re-created), so a source that is only partly captured -- a line skipped for a missing tax class, a refused description, a race loser that stopped
        // early -- stays visible instead of dropping off the list once its first charge exists (SECURITY and CODE_REVIEW gates). The billable-line counts here are
        // the Read-side twin of the capture path's rules (VisitBillingReader, DispenseBillableItemSource, SurgeryBillableItemSource): a cross-context list query
        // over those tables, as bounded-contexts.md allows; the capture itself reads a visit only through IVisitBillingReader (ADR-346).
        // A visit also needs a customer to bill (one finalised before the owner was stored cannot be captured and is not listed).
        var visitKind = ChargeSourceKind.Visit;
        var visits = await (
            from v in db.Set<CatEcommerce.Domain.Aggregates.Visit.VisitAggregate>().AsNoTracking()
            where !v.IsMarkedToDelete && v.BranchId == query.BranchId && v.Status == VisitStatus.Finalised && v.CustomerId != null
                  && v.FinalisedOn != null && v.FinalisedOn >= rangeFrom && v.FinalisedOn < rangeTo
            where v.Treatments.Count(t => t.IsBillable && t.Status == TreatmentLineStatus.Recorded)
                  + db.Set<CatEcommerce.Domain.Aggregates.Visit.DiagnosticOrderAggregate>().Count(o => o.VisitId == v.Id && !o.IsMarkedToDelete
                         && o.Status != DiagnosticOrderStatus.Cancelled && o.UnitPriceAmount != null)
                  > db.Set<ChargeAggregate>().Count(c => c.SourceKind == visitKind && c.SourceId == v.Id)
            orderby v.FinalisedOn
            select new { v.Id, CustomerId = v.CustomerId!.Value, On = v.FinalisedOn!.Value, Label = v.ServiceName }).ToListAsync();

        var dispenseKind = ChargeSourceKind.Dispense;
        var dispensings = await (
            from d in db.Set<CatEcommerce.Domain.Aggregates.Prescription.DispensingAggregate>().AsNoTracking()
            where !d.IsMarkedToDelete && d.BranchId == query.BranchId && d.Status == DispensingStatus.Dispensed
                  && d.DispensedOn >= rangeFrom && d.DispensedOn < rangeTo
            where d.Lines.Count(l => l.PriceSnapshot != null) > db.Set<ChargeAggregate>().Count(c => c.SourceKind == dispenseKind && c.SourceId == d.Id)
            orderby d.DispensedOn
            select new { d.Id, d.CustomerId, On = d.DispensedOn, Label = d.Label.MedicineLine }).ToListAsync();

        // A surgery's billable lines are counted the way SurgeryBillableItemSource bills them: the procedure fee when the procedure ended Completed, plus one line per
        // ORIGINAL drug entry whose latest correction (the leaf of its correction chain) still names a SKU and a positive quantity drawn. The chains are resolved in
        // memory over the candidate surgeries only (this branch, this date range, discharged, with an owner stored at discharge) -- a corrected drug is therefore
        // counted by what it was corrected TO, not by its original. A surgery is listed while it has fewer recorded charges than billable lines.
        var surgeryKind = ChargeSourceKind.Surgery;
        var candidates = await (
            from s in db.Set<CatEcommerce.Domain.Aggregates.Surgery.SurgeryAggregate>().AsNoTracking()
            where !s.IsMarkedToDelete && s.BranchId == query.BranchId && s.Status == SurgeryStatus.Discharged && s.OwnerAtDischargeId != null
                  && s.DischargedOn != null && s.DischargedOn >= rangeFrom && s.DischargedOn < rangeTo
            orderby s.DischargedOn
            select new
            {
                s.Id, CustomerId = s.OwnerAtDischargeId!.Value, On = s.DischargedOn!.Value, Label = s.Procedure.ServiceName,
                FeeLines = s.Outcome == ProcedureOutcome.Completed ? 1 : 0,
                Recorded = db.Set<ChargeAggregate>().Count(c => c.SourceKind == surgeryKind && c.SourceId == s.Id),
            }).ToListAsync();

        var candidateIds = candidates.Select(c => c.Id).ToList();
        var drugRows = candidateIds.Count == 0 ? [] : await db.AnaesthesiaLogEntries.AsNoTracking()
            .Where(e => candidateIds.Contains(e.SurgeryId) && !e.IsMarkedToDelete
                        && (e.Kind == AnaesthesiaEntryKind.Drug || (e.Kind == AnaesthesiaEntryKind.Correction && e.CorrectedKind == AnaesthesiaEntryKind.Drug)))
            .Select(e => new { e.SurgeryId, e.Id, e.Kind, e.CorrectsEntryId, e.LoggedOn, e.SkuId, e.QuantityDrawn })
            .ToListAsync();

        var drugLines = drugRows.GroupBy(r => r.SurgeryId).ToDictionary(g => g.Key, g =>
        {
            var latestCorrection = g.Where(r => r.CorrectsEntryId is not null).GroupBy(r => r.CorrectsEntryId!.Value)
                .ToDictionary(x => x.Key, x => x.OrderByDescending(r => r.LoggedOn).ThenByDescending(r => r.Id).First());
            var lines = 0;
            foreach (var original in g.Where(r => r.Kind == AnaesthesiaEntryKind.Drug))
            {
                var leaf = original;
                for (var hops = 0; latestCorrection.TryGetValue(leaf.Id, out var next) && hops < 100; hops++) leaf = next;
                if (leaf.SkuId is not null && leaf.QuantityDrawn is > 0m) lines++;
            }

            return lines;
        });

        var surgeries = candidates
            .Where(c => c.FeeLines + drugLines.GetValueOrDefault(c.Id) > c.Recorded)
            .Select(c => new { c.Id, c.CustomerId, c.On, c.Label }).ToList();

        IReadOnlyList<UncapturedSourceViewModel> result = rows
            .Select(r => new UncapturedSourceViewModel(appointmentKind.DisplayName, r.Id, r.CustomerId, r.CompletedOn, r.Label))
            .Concat(visits.Select(r => new UncapturedSourceViewModel(visitKind.DisplayName, r.Id, r.CustomerId, r.On, r.Label)))
            .Concat(dispensings.Select(r => new UncapturedSourceViewModel(dispenseKind.DisplayName, r.Id, r.CustomerId, r.On, r.Label)))
            .Concat(surgeries.Select(r => new UncapturedSourceViewModel(surgeryKind.DisplayName, r.Id, r.CustomerId, r.On, r.Label)))
            .OrderBy(r => r.CompletedOn).ThenBy(r => r.SourceKind, StringComparer.Ordinal).ThenBy(r => r.SourceId)
            .ToList();
        return new QueryResponse<IReadOnlyList<UncapturedSourceViewModel>> { Result = result };
    }
}

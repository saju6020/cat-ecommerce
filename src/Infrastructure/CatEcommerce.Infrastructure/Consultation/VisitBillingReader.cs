using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Consultation;

/// <summary>
/// HO-BIL-001 Phase J -- EMR's implementation of <see cref="IVisitBillingReader"/>. Untracked, projected reads. A treatment line is billable when it is
/// flagged billable, not reversed, and carries the price it was recorded at; a diagnostic line is an order that is not cancelled and has a recorded price
/// (an order made before Phase J has none, and is left out rather than priced at a guess). The tax class is returned as stored, null included.
/// </summary>
public sealed class VisitBillingReader(CatEcommerceDbContext db) : IVisitBillingReader
{
    public const string TreatmentRefPrefix = "treatment:";
    public const string DiagnosticRefPrefix = "diagnostic:";

    public async Task<VisitBillingFacts?> GetAsync(Guid visitId, CancellationToken ct = default)
    {
        var visit = await db.Set<VisitAggregate>().AsNoTracking()
            .Where(v => v.Id == visitId && !v.IsMarkedToDelete)
            .Select(v => new { v.Id, v.BranchId, v.CatId, v.AppointmentId, v.CustomerId, v.Status, v.FinalisedOn })
            .FirstOrDefaultAsync(ct);
        if (visit is null) return null;

        var finalised = visit.Status == VisitStatus.Finalised && visit.FinalisedOn is not null;
        if (!finalised)
            return new VisitBillingFacts(visit.Id, visit.BranchId, visit.CatId, visit.AppointmentId, visit.CustomerId, false, null, []);

        var treatments = await db.Set<VisitAggregate>().AsNoTracking().Where(v => v.Id == visitId)
            .SelectMany(v => v.Treatments)
            .Where(t => t.IsBillable && t.Status == TreatmentLineStatus.Recorded && t.UnitPriceAmount != null && t.UnitPriceCurrency != null)
            .OrderBy(t => t.RecordedOn).ThenBy(t => t.Position).ThenBy(t => t.Id)
            .Select(t => new { t.Id, t.Kind, t.Description, t.Quantity, Amount = t.UnitPriceAmount!.Value, Currency = t.UnitPriceCurrency!, t.TaxClassId, t.ClinicServiceId, t.SkuId })
            .ToListAsync(ct);

        var diagnostics = await db.Set<DiagnosticOrderAggregate>().AsNoTracking()
            .Where(o => o.VisitId == visitId && !o.IsMarkedToDelete && o.Status != DiagnosticOrderStatus.Cancelled && o.UnitPriceAmount != null && o.UnitPriceCurrency != null)
            .OrderBy(o => o.OrderedOn).ThenBy(o => o.Id)
            .Select(o => new { o.Id, o.Kind, o.Description, Amount = o.UnitPriceAmount!.Value, Currency = o.UnitPriceCurrency!, o.TaxClassId })
            .ToListAsync(ct);

        var lines = new List<VisitBillingLine>(treatments.Count + diagnostics.Count);
        lines.AddRange(treatments.Select(t => new VisitBillingLine(
            TreatmentRefPrefix + t.Id.ToString("N"), VisitBillableLineKind.Of(t.Kind), t.Description, t.Quantity,
            new Money(t.Amount, t.Currency), t.TaxClassId, t.ClinicServiceId, t.SkuId)));
        lines.AddRange(diagnostics.Select(o => new VisitBillingLine(
            DiagnosticRefPrefix + o.Id.ToString("N"), VisitBillableLineKind.Diagnostic, DiagnosticDescription(o.Kind, o.Description), 1m,
            new Money(o.Amount, o.Currency), o.TaxClassId, null, null)));

        return new VisitBillingFacts(visit.Id, visit.BranchId, visit.CatId, visit.AppointmentId, visit.CustomerId, true, visit.FinalisedOn, lines);
    }

    /// <summary>The words printed on the invoice: the kind of test, plus the study the vet described where the kind is open-ended (imaging, other).</summary>
    public static string DiagnosticDescription(DiagnosticKind kind, string? description)
    {
        var label = kind == DiagnosticKind.BloodWork ? "Blood work"
            : kind == DiagnosticKind.Urinalysis ? "Urinalysis"
            : kind == DiagnosticKind.FelvFivTest ? "FeLV/FIV test"
            : kind == DiagnosticKind.Imaging ? "Imaging"
            : "Diagnostic test";

        var detail = description?.Trim();
        if (string.IsNullOrEmpty(detail) || (kind != DiagnosticKind.Imaging && kind != DiagnosticKind.Other)) return label;
        if (detail.Length > 200) detail = detail[..200];
        return $"{label}: {detail}";
    }
}

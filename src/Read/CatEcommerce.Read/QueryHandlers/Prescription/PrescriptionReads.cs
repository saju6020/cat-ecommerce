using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Prescription;

/// <summary>A prescription's list columns, projected -- never the aggregate (.ai/read-side-pattern.md §4, ADR-012). A class with settable members so EF can
/// compose over it.</summary>
internal sealed class PrescriptionRow
{
    public Guid Id { get; set; }
    public Guid CatId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid VisitId { get; set; }
    public string PrescriptionNumber { get; set; } = default!;
    public string MedicineName { get; set; } = default!;
    public string MedicineStrengthText { get; set; } = default!;
    public MedicineForm MedicineForm { get; set; } = default!;
    public bool IsControlled { get; set; }
    public DispensingUnit DispensingUnit { get; set; } = default!;
    public decimal DoseAmount { get; set; }
    public DoseUnit DoseUnit { get; set; } = default!;
    public AdministrationRoute Route { get; set; } = default!;
    public DoseFrequency Frequency { get; set; } = default!;
    public int DurationDays { get; set; }
    public decimal Quantity { get; set; }
    public PrescriptionStatus Status { get; set; } = default!;
    public DateOnly StartsOn { get; set; }
    public DateOnly? LastDispensedOn { get; set; }
    public bool IsRepeatable { get; set; }
    public int RepeatsAuthorised { get; set; }
    public int SuppliesDispensed { get; set; }
    public DateOnly ValidUntil { get; set; }
    public string PrescriberName { get; set; } = "";
    public DateTimeOffset IssuedOn { get; set; }
    public bool HasAcknowledgedWarnings { get; set; }
}

internal static class PrescriptionReads
{
    /// <summary>Projects prescriptions (with the prescriber's name) straight to rows in one query.</summary>
    public static IQueryable<PrescriptionRow> Rows(CatEcommerceDbContext db, IQueryable<PrescriptionAggregate> prescriptions) =>
        from p in prescriptions.AsNoTracking()
        join s in db.StaffAccounts.AsNoTracking() on p.PrescriberId equals s.Id into staff
        from s in staff.DefaultIfEmpty()
        select new PrescriptionRow
        {
            Id = p.Id, CatId = p.CatId, CustomerId = p.CustomerId, VisitId = p.VisitId, PrescriptionNumber = p.PrescriptionNumber,
            MedicineName = p.MedicineSnapshot.Name, MedicineStrengthText = p.MedicineSnapshot.StrengthText, MedicineForm = p.MedicineSnapshot.Form,
            IsControlled = p.MedicineSnapshot.IsControlled, DispensingUnit = p.MedicineSnapshot.DispensingUnit,
            DoseAmount = p.Dose.Amount, DoseUnit = p.Dose.Unit, Route = p.Route, Frequency = p.Frequency, DurationDays = p.DurationDays, Quantity = p.Quantity,
            Status = p.Status, StartsOn = p.StartsOn, LastDispensedOn = p.LastDispensedOn, IsRepeatable = p.IsRepeatable, RepeatsAuthorised = p.RepeatsAuthorised,
            SuppliesDispensed = p.SuppliesDispensed, ValidUntil = p.ValidUntil, PrescriberName = s != null ? s.FullName : "", IssuedOn = p.IssuedOn,
            HasAcknowledgedWarnings = p.SafetyRecord.AcknowledgedBy != null
        };

    public static bool IsActive(PrescriptionRow r, DateOnly today) =>
        PrescriptionActivityRules.IsActive(r.Status, r.StartsOn, r.LastDispensedOn, r.DurationDays, r.RepeatsAuthorised, r.SuppliesDispensed, r.ValidUntil, today);

    public static PrescriptionStatus EffectiveStatus(PrescriptionRow r, DateOnly today) =>
        PrescriptionActivityRules.EffectiveStatus(r.Status, r.ValidUntil, r.RepeatsAuthorised, r.SuppliesDispensed, today);

    public static PrescriptionSummaryViewModel ToSummary(PrescriptionRow r, DateOnly today) => new(
        r.Id, r.PrescriptionNumber, new PrescriptionMedicineViewModel(r.MedicineName, r.MedicineStrengthText, r.MedicineForm.DisplayName, r.IsControlled),
        QuantityViewModel.Of(r.DoseAmount, r.DoseUnit.DisplayName), r.Route.DisplayName, r.Frequency.Label, r.DurationDays,
        QuantityViewModel.Of(r.Quantity, r.DispensingUnit.DisplayName), EffectiveStatus(r, today).DisplayName, IsActive(r, today),
        PrescriptionActivityRules.CourseEndsOn(r.StartsOn, r.LastDispensedOn, r.DurationDays),
        PrescriptionActivityRules.RemainingDays(r.StartsOn, r.LastDispensedOn, r.DurationDays, today), r.SuppliesDispensed, 1 + r.RepeatsAuthorised,
        r.ValidUntil, r.PrescriberName, r.IssuedOn, r.HasAcknowledgedWarnings);

    public static async Task<IReadOnlyDictionary<Guid, string>> StaffNamesAsync(CatEcommerceDbContext db, IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var distinct = ids.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();

        var rows = await db.Set<StaffAccountAggregate>().AsNoTracking().Where(s => distinct.Contains(s.Id)).Select(s => new { s.Id, s.FullName }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => r.FullName);
    }

    public static string NameOf(IReadOnlyDictionary<Guid, string> names, Guid? id) => id is { } value && names.TryGetValue(value, out var name) ? name : "";
}

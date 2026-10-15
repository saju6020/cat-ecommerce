using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Pharmacy;

/// <summary>One interaction row as projected from <c>pharmacy.formulary_interaction</c>.</summary>
public sealed class InteractionRow
{
    public Guid? InteractsWithMedicineId { get; set; }
    public string? InteractsWithTerm { get; set; }
    public InteractionSeverity Severity { get; set; } = default!;
    public string Note { get; set; } = default!;
}

/// <summary>A formulary medicine's columns, projected -- never the aggregate (.ai/read-side-pattern.md §4, ADR-012). A class with settable members so
/// EF can compose over it.</summary>
public sealed class FormularyRow
{
    public Guid MedicineId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public MedicineForm Form { get; set; } = default!;
    public decimal StrengthAmount { get; set; }
    public StrengthUnit StrengthUnit { get; set; } = default!;
    public decimal StrengthPerQuantity { get; set; }
    public DispensingUnit StrengthPerUnit { get; set; } = default!;
    public AdministrationRoute DefaultRoute { get; set; } = default!;
    public int[] AllowedRouteValues { get; set; } = [];
    public DosingBasis DosingBasis { get; set; } = default!;
    public decimal? DoseMinMgPerKg { get; set; }
    public decimal? DoseMaxMgPerKg { get; set; }
    public string Contraindications { get; set; } = "";
    public string[] ActiveIngredients { get; set; } = [];
    public string[] DrugClasses { get; set; } = [];
    public List<InteractionRow> Interactions { get; set; } = [];
    public bool IsControlled { get; set; }
    public string? ControlledSchedule { get; set; }
    public DispensingUnit DispensingUnit { get; set; } = default!;
    public List<Guid> LinkedSkuIds { get; set; } = [];
    public bool IsActive { get; set; }
    public int Version { get; set; }

    public FormularyMedicineSnapshot ToSnapshot() => new(
        MedicineId, Code, Name, Form, new Strength(StrengthAmount, StrengthUnit, StrengthPerQuantity, StrengthPerUnit), DefaultRoute,
        AllowedRouteValues.Select(Enumeration.FromValue<AdministrationRoute>).ToList(), DosingBasis,
        DoseMinMgPerKg is { } min && DoseMaxMgPerKg is { } max ? new DoseRange(min, max) : null, Contraindications, ActiveIngredients, DrugClasses,
        Interactions.Select(i => new InteractionSnapshot(i.InteractsWithMedicineId, i.InteractsWithTerm, i.Severity, i.Note)).ToList(),
        IsControlled, ControlledSchedule, DispensingUnit, LinkedSkuIds, IsActive);
}

public static class FormularyProjections
{
    /// <summary>Projects medicines straight to rows (one query), no aggregate rehydrated.</summary>
    public static IQueryable<FormularyRow> AsRows(this IQueryable<FormularyMedicineAggregate> query) =>
        query.AsNoTracking().Select(m => new FormularyRow
        {
            MedicineId = m.Id,
            Code = m.Code,
            Name = m.Name,
            Form = m.Form,
            StrengthAmount = m.Strength.Amount,
            StrengthUnit = m.Strength.Unit,
            StrengthPerQuantity = m.Strength.PerQuantity,
            StrengthPerUnit = m.Strength.PerUnit,
            DefaultRoute = m.DefaultRoute,
            AllowedRouteValues = m.AllowedRouteValues,
            DosingBasis = m.DosingBasis,
            DoseMinMgPerKg = m.DoseRange == null ? null : m.DoseRange.MinMgPerKg,
            DoseMaxMgPerKg = m.DoseRange == null ? null : m.DoseRange.MaxMgPerKg,
            Contraindications = m.Contraindications,
            ActiveIngredients = m.ActiveIngredients,
            DrugClasses = m.DrugClasses,
            Interactions = m.Interactions.Select(i => new InteractionRow
            { InteractsWithMedicineId = i.InteractsWithMedicineId, InteractsWithTerm = i.InteractsWithTerm, Severity = i.Severity, Note = i.Note }).ToList(),
            IsControlled = m.IsControlled,
            ControlledSchedule = m.ControlledSchedule,
            DispensingUnit = m.DispensingUnit,
            LinkedSkuIds = m.LinkedSkus.Select(l => l.SkuId).ToList(),
            IsActive = m.IsActive,
            Version = m.Version
        });

    public static async Task<List<FormularyMedicineSnapshot>> ToSnapshotsAsync(this IQueryable<FormularyMedicineAggregate> query, CancellationToken ct) =>
        (await query.AsRows().ToListAsync(ct)).Select(r => r.ToSnapshot()).ToList();
}

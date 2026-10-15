using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-PHM-001 §11.3 -- static <c>ToDomainDto()</c> extensions for the formulary commands, plus the helpers every Pharmacy mapper shares.</summary>
public static class FormularyMapper
{
    public static PharmacyActor ToPharmacyActor(this Command command) => new(command.UserContext!, command.CorrelationId);

    /// <summary>Case-insensitive, by display name. Every enumeration field is checked by its validator (<c>MustBeValidEnumeration</c>) before a handler
    /// runs, so this only throws for a value that bypassed validation.</summary>
    public static T ParseEnum<T>(string value) where T : Enumeration =>
        EnumerationParsing.TryParse<T>(value, out var parsed) && parsed is not null
            ? parsed
            : throw new ArgumentException($"'{value}' is not a valid {typeof(T).Name}.", nameof(value));

    public static FormularyMedicineDto ToDomainDto(this FormularyMedicineCommandBase c) => new(
        c.Name,
        c.ActiveIngredients,
        c.DrugClasses,
        ParseEnum<MedicineForm>(c.Form),
        new StrengthInput(c.Strength.Amount, ParseEnum<StrengthUnit>(c.Strength.Unit), c.Strength.PerQuantity, ParseEnum<DispensingUnit>(c.Strength.PerUnit)),
        ParseEnum<AdministrationRoute>(c.DefaultRoute),
        c.AllowedRoutes.Select(r => ParseEnum<AdministrationRoute>(r)).ToList(),
        ParseEnum<DosingBasis>(c.DosingBasis),
        c.DoseRange is null ? null : new DoseRangeInput(c.DoseRange.MinMgPerKg, c.DoseRange.MaxMgPerKg),
        c.Contraindications,
        c.Interactions.Select(i => new InteractionInput(i.InteractsWithMedicineId, i.InteractsWithTerm, ParseEnum<InteractionSeverity>(i.Severity), i.Note)).ToList(),
        ParseEnum<DispensingUnit>(c.DispensingUnit),
        c.LinkedSkuIds,
        c.ToPharmacyActor());
}

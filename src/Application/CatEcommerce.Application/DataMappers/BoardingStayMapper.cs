using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-BRD-001 section 11.3 -- static mappers for the stay commands. Enum-typed fields were accepted by their validators before a handler runs.</summary>
public static class BoardingStayMapper
{
    public static CareProfile ToCareProfile(this CareProfileInput input) =>
        new(
            input.Feedings.Select(f => new FeedingPlan(f.Time, f.Portion.Trim(), BoardingUnitMapper.ParseEnum<FoodSource>(f.FoodSource), f.FoodDescription.Trim(), f.HouseFoodSkuId)).ToList(),
            input.Medications.Select(m => new MedicationPlan(m.Name.Trim(), m.Dose.Trim(), m.Route.Trim(), m.Times, m.Instructions?.Trim(), m.FromOn, m.ToOn)).ToList(),
            BoardingUnitMapper.ParseEnum<LitterType>(input.LitterType),
            input.LitterNote?.Trim(),
            input.VetVisitAuthorised,
            input.VetSpendingLimit,
            new EmergencyContact(input.EmergencyContact.Name.Trim(), input.EmergencyContact.Phone.Trim(), input.EmergencyContact.Relationship?.Trim()),
            input.Notes?.Trim());
}

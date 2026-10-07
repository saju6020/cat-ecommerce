using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.SharedDto.Cat;

public sealed record AllergyInput(
    string Allergen,
    string Reaction,
    AllergySeverity Severity,
    DateOnly RecordedOn);

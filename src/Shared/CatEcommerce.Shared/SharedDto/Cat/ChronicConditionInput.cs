namespace CatEcommerce.Shared.SharedDto.Cat;

public sealed record ChronicConditionInput(
    string Condition,
    DateOnly? DiagnosedOn,
    string? Notes);

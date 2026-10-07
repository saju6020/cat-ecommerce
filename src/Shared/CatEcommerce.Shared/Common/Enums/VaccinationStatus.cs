namespace CatEcommerce.Shared.Common.Enums;

public sealed class VaccinationStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly VaccinationStatus Current = new(1, nameof(Current));
    public static readonly VaccinationStatus DueSoon = new(2, nameof(DueSoon));
    public static readonly VaccinationStatus Overdue = new(3, nameof(Overdue));
}

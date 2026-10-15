using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Sources;

/// <summary>The display name of a persisted enumeration value, for the report cells. An unknown value prints as its number rather than failing the whole report.</summary>
public static class ReportLabels
{
    public static string Of<T>(int value) where T : Enumeration =>
        Enumeration.GetAll<T>().FirstOrDefault(e => e.Value == value)?.DisplayName ?? value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string? Of<T>(int? value) where T : Enumeration => value is null ? null : Of<T>(value.Value);
}

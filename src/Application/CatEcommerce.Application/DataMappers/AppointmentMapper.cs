using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-APT-001 §11.3 -- enum parsing shared by every Clinic Appointments command handler.
/// Matches <c>AdministrationMapper</c>'s own <c>ParseEnum</c>/<c>ParseNullableEnum</c> pattern:
/// the command's own <c>MustBeValidEnumeration</c> validator rule has already rejected an
/// unrecognised value before this runs.</summary>
public static class AppointmentMapper
{
    public static T ParseEnum<T>(string value) where T : Enumeration
    {
        if (!EnumerationParsing.TryParse<T>(value, out var result) || result is null)
        {
            throw new InvalidOperationException(
                $"'{value}' is not a recognised {typeof(T).Name}. This should have been rejected by the command's own validator before the mapper ever ran.");
        }

        return result;
    }

    public static T? ParseNullableEnum<T>(string? value) where T : Enumeration =>
        string.IsNullOrWhiteSpace(value) ? null : ParseEnum<T>(value);
}

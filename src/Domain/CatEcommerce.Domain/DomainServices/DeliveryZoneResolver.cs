using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.8, US-ORD-06 AC2 -- pure. Which delivery zone serves an address: the <b>active</b> zones with a coverage row for the address's district
/// (compared case- and whitespace-insensitively) and either no postal codes (the whole district) or the address's postal code in the list. A
/// postal-code-specific match beats a district-wide one; then the lowest <c>Priority</c>; then the name. No match is
/// <c>AddressOutsideServedZones</c> (the caller's decision: the quote then offers pickup).
/// </summary>
public static class DeliveryZoneResolver
{
    public static ResolvableZone? Resolve(IEnumerable<ResolvableZone> zones, string? district, string? postalCode)
    {
        var wanted = NormaliseDistrict(district);
        if (wanted.Length == 0) return null;

        var code = postalCode?.Trim();

        return zones
            .Where(z => z.IsActive)
            .Select(z => (Zone: z, Specific: MatchKind(z, wanted, code)))
            .Where(m => m.Specific is not null)
            .OrderByDescending(m => m.Specific == true) // postal-code-specific first
            .ThenBy(m => m.Zone.Priority)
            .ThenBy(m => m.Zone.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Zone.ZoneId)
            .Select(m => m.Zone)
            .FirstOrDefault();
    }

    /// <summary>A district compared as the service compares it: trimmed, inner whitespace collapsed, upper-cased invariantly.</summary>
    public static string NormaliseDistrict(string? district) =>
        string.Join(' ', (district ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToUpperInvariant();

    /// <summary><c>true</c> = matched by a listed postal code, <c>false</c> = matched district-wide, <c>null</c> = no match.</summary>
    private static bool? MatchKind(ResolvableZone zone, string wantedDistrict, string? postalCode)
    {
        bool? best = null;
        foreach (var coverage in zone.Coverage.Where(c => NormaliseDistrict(c.District) == wantedDistrict))
        {
            if (coverage.PostalCodes.Count == 0)
            {
                best ??= false;
                continue;
            }

            if (postalCode is { Length: > 0 } && coverage.PostalCodes.Any(p => string.Equals(p.Trim(), postalCode, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return best;
    }
}

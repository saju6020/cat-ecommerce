namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>Bound from appsettings.json section "CatProfile" -- HO-CAT-001 section 12.1.</summary>
public sealed class CatProfileOptions
{
    public const string SectionName = "CatProfile";

    /// <summary>FR-CAT-7 -- default window for the DueSoon vaccination status.</summary>
    public int VaccinationDueSoonWindowDays { get; set; } = 30;

    public int DefaultParasiteIntervalDays { get; set; } = 90;
}

using System.ComponentModel.DataAnnotations;

namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>
/// Bound from appsettings.json section "AuditExport" (gate fix 2026-09-26, TEST gate I-E3):
/// <c>AuditExportRules.MaxRows</c> was a hard-coded <c>const int</c> with no seam a test factory
/// could use to lower it, so "export over the cap -&gt; 413" could not be exercised without
/// generating 50,001 real rows against Postgres. The production default (50,000, the same value
/// <c>AuditExportRules.MaxRows</c> still documents) is unchanged; a test factory now overrides just
/// this one value via configuration -- the same technique <c>AccountsApiFactory
/// .WithTightRateLimits()</c> already uses for rate limits.
///
/// Gate fix 2026-09-26 (CODE_REVIEW seventh pass, MEDIUM): <c>[Range(1, AuditExportRules.MaxRows)]</c>
/// -- validated at startup (<c>AdministrationModule</c>'s <c>ValidateDataAnnotations().ValidateOnStart()</c>,
/// the same pattern <c>AccountOptions</c> already uses) -- so a misconfigured value (zero, negative,
/// or above the product's own hard ceiling) fails the host at boot, rather than either silently
/// disabling the cap (a huge value, or one near <c>int.MaxValue</c> overflowing <c>MaxRows + 1</c> in
/// <c>AdminAuditEntriesController.Export</c>) or reaching the query pipeline with a negative
/// <c>PageSize</c> the platform was never meant to receive. An operator may still LOWER the cap below
/// the product default, but never raise it past what the product itself ships with.
/// </summary>
public sealed class AuditExportOptions
{
    public const string SectionName = "AuditExport";

    [Range(1, CatEcommerce.Shared.Common.Constants.AuditExportRules.MaxRows,
        ErrorMessage = "AuditExport:MaxRows must be a positive integer, at most the product's own cap (AuditExportRules.MaxRows, 50,000).")]
    public int MaxRows { get; set; } = CatEcommerce.Shared.Common.Constants.AuditExportRules.MaxRows;
}

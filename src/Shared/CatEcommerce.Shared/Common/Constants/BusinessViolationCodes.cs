namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Codes an aggregate raises through DomainRuleViolationException.
///
/// <c>partial static</c> so each functional area (ADR-341) adds its own codes in its own
/// <c>BusinessViolationCodes.[Area].cs</c> file (e.g. <c>BusinessViolationCodes.Shop.cs</c>)
/// instead of editing this file. See ADR-341 and <c>.ai/decisions/adr.md</c>.
/// </summary>
public static partial class BusinessViolationCodes
{
    /// <summary>Gate fix 2026-09-26 (SEC LOW, hardening round). Used by the API-wide
    /// <c>NulByteRejectionFilter</c> (Api/Filters), which applies to every controller in the host --
    /// declared here, in the base file, rather than in any one area's own partial (integrator move,
    /// 2026-09-26: it was originally declared in <c>BusinessViolationCodes.Administration.cs</c>,
    /// the area that built the filter, the same precedent as <c>ClinicalAuditEntityTypes</c> --
    /// moved here alongside the filter's own registration, now also host-wide in <c>Program.cs</c>,
    /// per HO-ADM-001-ACCEPTANCE's integrator note and HO-ADM-001-ARCHITECTURE's MEDIUM 1).</summary>
    public const string ContainsControlCharacter = nameof(ContainsControlCharacter);
}

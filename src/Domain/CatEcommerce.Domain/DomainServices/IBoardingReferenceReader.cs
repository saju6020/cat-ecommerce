namespace CatEcommerce.Domain.DomainServices;

public sealed record BoardingBranchInfo(Guid BranchId, string TimeZoneId, bool IsActive);

/// <summary>
/// HO-BRD-001 ADR-232 (renumbered ADR-365): the facts Boarding reads from other contexts -- a branch's time zone and state, a tax class's existence --
/// through ONE read port, so no Boarding command handler injects an Administration repository (U-X4). Implemented read-only (<c>AsNoTracking</c>) in
/// <c>Infrastructure/Boarding</c>.
/// </summary>
public interface IBoardingReferenceReader
{
    Task<BoardingBranchInfo?> GetBranchAsync(Guid branchId, CancellationToken ct = default);

    /// <summary>Whether the tax class exists and is active.</summary>
    Task<bool> TaxClassIsActiveAsync(Guid taxClassId, CancellationToken ct = default);

    /// <summary>The active core vaccines the vaccination gate checks every cat against (HO-CAT-001 Q4).</summary>
    Task<IReadOnlyList<VaccinationRuleVaccine>> GetActiveCoreVaccinesAsync(CancellationToken ct = default);

    /// <summary>The branch-local calendar date of <paramref name="instant"/>.</summary>
    Task<DateOnly?> LocalDateAsync(Guid branchId, DateTimeOffset instant, CancellationToken ct = default);
}

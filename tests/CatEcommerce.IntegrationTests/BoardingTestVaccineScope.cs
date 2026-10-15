using CatEcommerce.Domain.DomainServices;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// Test-only decorator (the FilesApiFactory pattern): the Boarding vaccination gate checks a cat against EVERY active core vaccine in the shared test database, and other areas' tests create core
/// vaccines of their own (one had a validity of a month, which blocks any stay that outlives it). The Boarding fixture seeds its OWN core vaccines (code prefix <see cref="CodePrefix"/>, validity
/// 12 months) and the gate in the Boarding host looks at those only -- so every test seeds its own vaccines on its own clock, needs no retry and no administrator override, and the gate itself
/// stays covered by its dedicated tests.
/// </summary>
public sealed class BoardingTestVaccineScope(IBoardingReferenceReader inner) : IBoardingReferenceReader
{
    public const string CodePrefix = "BRD";

    public Task<BoardingBranchInfo?> GetBranchAsync(Guid branchId, CancellationToken ct = default) => inner.GetBranchAsync(branchId, ct);

    public Task<bool> TaxClassIsActiveAsync(Guid taxClassId, CancellationToken ct = default) => inner.TaxClassIsActiveAsync(taxClassId, ct);

    public async Task<IReadOnlyList<VaccinationRuleVaccine>> GetActiveCoreVaccinesAsync(CancellationToken ct = default) =>
        (await inner.GetActiveCoreVaccinesAsync(ct)).Where(v => v.Code.StartsWith(CodePrefix, StringComparison.Ordinal)).ToList();

    public Task<DateOnly?> LocalDateAsync(Guid branchId, DateTimeOffset instant, CancellationToken ct = default) => inner.LocalDateAsync(branchId, instant, ct);
}

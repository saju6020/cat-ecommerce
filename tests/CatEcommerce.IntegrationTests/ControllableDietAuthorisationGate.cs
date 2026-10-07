using System.Collections.Concurrent;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.IntegrationTests;

/// <summary>The authorisations a test has "issued" and the gate's notion of today. Shared by every
/// scoped <see cref="ControllableDietAuthorisationGate"/> the factory creates.</summary>
public sealed class ControllableDietAuthorisations
{
    public sealed record Authorisation(Guid CatId, Guid? SkuId, DateOnly ValidTo);

    private readonly ConcurrentDictionary<Guid, List<Authorisation>> _byCustomer = new();

    /// <summary>The gate evaluates against this date -- a test moves it to prove the contract is
    /// evaluated at the call instant (US-SHP-10 AC3).</summary>
    public Func<DateOnly> Today { get; set; } = () => DateOnly.FromDateTime(DateTime.UtcNow);

    public void Authorise(Guid customerId, Guid catId, DateOnly validTo, Guid? skuId = null) =>
        _byCustomer.AddOrUpdate(customerId, _ => [new Authorisation(catId, skuId, validTo)], (_, list) => { list.Add(new Authorisation(catId, skuId, validTo)); return list; });

    public IReadOnlyList<Authorisation> For(Guid customerId) =>
        _byCustomer.TryGetValue(customerId, out var list) ? list.ToList() : [];
}

/// <summary>
/// HO-SHP-001 §16.1 rule 4 -- a stand-in for HO-PHM-001's real gate. It answers from the
/// authorisations a test issued, and applies the same rules HO-PHM-001 ADR-149 gives the real
/// gate -- expiry against today, the cat must be alive, the cat must still be this customer's,
/// and an authorisation naming another SKU does not cover this one -- by reading the real
/// <c>clinic.cat</c> row. The shipped fail-closed <c>DenyAllDietAuthorisationGate</c> is exercised
/// separately (I-F8) by NOT installing this replacement.
/// </summary>
public sealed class ControllableDietAuthorisationGate(ControllableDietAuthorisations state, CatEcommerceDbContext db) : IDietAuthorisationGate
{
    public async Task<DietGateResult> CheckAsync(Guid customerId, Guid catId, Guid skuId, CancellationToken ct)
    {
        var cat = await db.Set<CatAggregate>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == catId, ct);
        if (cat is null || cat.OwnerId != customerId) return Result(DietGateOutcome.CatNotOwned);
        if (cat.LifeStatus == CatLifeStatus.Deceased) return Result(DietGateOutcome.CatDeceased);

        var matching = state.For(customerId).Where(a => a.CatId == catId && (a.SkuId is null || a.SkuId == skuId)).ToList();
        if (matching.Count == 0) return Result(DietGateOutcome.NoAuthorisation);

        var today = state.Today();
        return matching.Any(a => a.ValidTo >= today)
            ? new DietGateResult(DietGateOutcome.Authorised, Guid.NewGuid(), matching.Max(a => a.ValidTo), "Authorised.")
            : Result(DietGateOutcome.AuthorisationExpired);
    }

    public async Task<IReadOnlyList<AuthorisedCat>> GetAuthorisedCatsAsync(Guid customerId, Guid skuId, CancellationToken ct)
    {
        var today = state.Today();
        var result = new List<AuthorisedCat>();

        foreach (var group in state.For(customerId).Where(a => (a.SkuId is null || a.SkuId == skuId) && a.ValidTo >= today).GroupBy(a => a.CatId))
        {
            var cat = await db.Set<CatAggregate>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == group.Key, ct);
            if (cat is null || cat.OwnerId != customerId || cat.LifeStatus != CatLifeStatus.Alive) continue;

            result.Add(new AuthorisedCat(cat.Id, cat.Name, Guid.NewGuid(), group.Max(a => a.ValidTo)));
        }

        return result;
    }

    private static DietGateResult Result(DietGateOutcome outcome) => new(outcome, null, null, outcome.DisplayName);
}

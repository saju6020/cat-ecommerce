using System.Globalization;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Pharmacy;

/// <summary>
/// HO-PHM-001 §8.10 (ADR-149, US-PHM-08 AC1-AC2) -- the real <see cref="IDietAuthorisationGate"/>. Read-only. "Today" is the main branch's local date computed on EVERY
/// call, so the product locks again at midnight whether or not the expiry sweep has run. Evaluation order: the SKU's product does not require a prescription (NotRequired) ->
/// the cat is not the customer's (CatNotOwned) -> deceased (CatDeceased) -> an Active authorisation with ValidFrom &lt;= today &lt;= ValidTo (Authorised) -> one that exists but
/// whose last day has passed, whatever its stored status short of Revoked (AuthorisationExpired) -> NoAuthorisation. A SKU the catalogue does not know fails closed
/// (NoAuthorisation). Messages are owner-facing and carry no clinical detail.
/// </summary>
public sealed class DietAuthorisationGate(CatEcommerceDbContext db, ICatalogueReader catalogue, IPharmacyClock clock) : IDietAuthorisationGate
{
    private const string NoAuthorisationMessage = "This food needs a vet's authorisation for your cat. Book a consultation to get one.";

    private sealed record CatFacts(Guid Id, Guid OwnerId, string Name, CatLifeStatus LifeStatus);

    private sealed record AuthorisationFacts(Guid Id, Guid ProductId, DateOnly ValidFrom, DateOnly ValidTo, DietAuthorisationStatus Status);

    public async Task<DietGateResult> CheckAsync(Guid customerId, Guid catId, Guid skuId, CancellationToken ct)
    {
        var sku = (await catalogue.GetSkusAsync([skuId], ct)).FirstOrDefault();
        if (sku is null) return Denied();
        if (!sku.RequiresPrescription) return new DietGateResult(DietGateOutcome.NotRequired, null, null, "");

        var cat = await LoadCatAsync(catId, ct);
        var authorisations = cat is null ? [] : await LoadAuthorisationsAsync(catId, [sku.ProductId], ct);
        return Evaluate(customerId, cat, authorisations.Where(a => a.ProductId == sku.ProductId).ToList(), await clock.MainBranchTodayAsync(ct));
    }

    /// <summary>One set-based read for the cat and its authorisations, however many SKUs are asked about (HO-SHP-001 backlog row 20).</summary>
    public async Task<IReadOnlyDictionary<Guid, DietGateResult>> CheckManyAsync(Guid customerId, Guid catId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
    {
        var distinct = skuIds.Distinct().ToList();
        var results = new Dictionary<Guid, DietGateResult>();
        if (distinct.Count == 0) return results;

        var skus = (await catalogue.GetSkusAsync(distinct, ct)).ToDictionary(s => s.SkuId);
        var productIds = skus.Values.Where(s => s.RequiresPrescription).Select(s => s.ProductId).Distinct().ToList();

        var today = await clock.MainBranchTodayAsync(ct);
        var cat = productIds.Count == 0 ? null : await LoadCatAsync(catId, ct);
        var authorisations = cat is null ? [] : await LoadAuthorisationsAsync(catId, productIds, ct);

        foreach (var skuId in distinct)
        {
            if (!skus.TryGetValue(skuId, out var sku)) results[skuId] = Denied();
            else if (!sku.RequiresPrescription) results[skuId] = new DietGateResult(DietGateOutcome.NotRequired, null, null, "");
            else results[skuId] = Evaluate(customerId, cat, authorisations.Where(a => a.ProductId == sku.ProductId).ToList(), today);
        }

        return results;
    }

    public async Task<IReadOnlyList<AuthorisedCat>> GetAuthorisedCatsAsync(Guid customerId, Guid skuId, CancellationToken ct)
    {
        var sku = (await catalogue.GetSkusAsync([skuId], ct)).FirstOrDefault();
        if (sku is null || !sku.RequiresPrescription) return [];

        var today = await clock.MainBranchTodayAsync(ct);
        var productId = sku.ProductId;

        var rows = await db.DietAuthorisations.AsNoTracking()
            .Where(a => a.ProductId == productId && a.Status == DietAuthorisationStatus.Active && a.ValidFrom <= today && today <= a.ValidTo)
            .Join(db.Cats.AsNoTracking().Where(c => c.OwnerId == customerId && c.LifeStatus == CatLifeStatus.Alive), a => a.CatId, c => c.Id,
                (a, c) => new { AuthorisationId = a.Id, CatId = c.Id, CatName = c.Name, a.ValidTo })
            .OrderBy(x => x.CatName).ThenBy(x => x.CatId)
            .ToListAsync(ct);

        return rows.Select(r => new AuthorisedCat(r.CatId, r.CatName, r.AuthorisationId, r.ValidTo)).ToList();
    }

    private async Task<CatFacts?> LoadCatAsync(Guid catId, CancellationToken ct) =>
        await db.Cats.AsNoTracking().Where(c => c.Id == catId).Select(c => new CatFacts(c.Id, c.OwnerId, c.Name, c.LifeStatus)).FirstOrDefaultAsync(ct);

    private async Task<List<AuthorisationFacts>> LoadAuthorisationsAsync(Guid catId, IReadOnlyCollection<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.ToList();
        return await db.DietAuthorisations.AsNoTracking()
            .Where(a => a.CatId == catId && ids.Contains(a.ProductId) && a.Status != DietAuthorisationStatus.Revoked)
            .Select(a => new AuthorisationFacts(a.Id, a.ProductId, a.ValidFrom, a.ValidTo, a.Status))
            .ToListAsync(ct);
    }

    private static DietGateResult Evaluate(Guid customerId, CatFacts? cat, IReadOnlyList<AuthorisationFacts> forProduct, DateOnly today)
    {
        if (cat is null || cat.OwnerId != customerId)
            return new DietGateResult(DietGateOutcome.CatNotOwned, null, null, "Choose one of your own cats for this food.");

        if (cat.LifeStatus != CatLifeStatus.Alive)
            return new DietGateResult(DietGateOutcome.CatDeceased, null, null, $"{cat.Name} is no longer with us.");

        var current = forProduct.Where(a => a.Status == DietAuthorisationStatus.Active && a.ValidFrom <= today && today <= a.ValidTo).OrderByDescending(a => a.ValidTo).FirstOrDefault();
        if (current is not null) return new DietGateResult(DietGateOutcome.Authorised, current.Id, current.ValidTo, "");

        var ended = forProduct.Where(a => a.ValidTo < today).OrderByDescending(a => a.ValidTo).FirstOrDefault();
        if (ended is not null)
        {
            var date = ended.ValidTo.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
            return new DietGateResult(DietGateOutcome.AuthorisationExpired, ended.Id, ended.ValidTo, $"{cat.Name}'s authorisation for this food ended on {date}.");
        }

        return Denied();
    }

    private static DietGateResult Denied() => new(DietGateOutcome.NoAuthorisation, null, null, NoAuthorisationMessage);
}

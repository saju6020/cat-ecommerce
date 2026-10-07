namespace CatEcommerce.Domain.Models;

/// <summary>
/// The return shape of <c>IDietAuthorisationGate.GetAuthorisedCatsAsync</c> (HO-PHM-001 §8.10) --
/// listing which of a customer's cats may buy a given prescription-diet SKU (used by the shop's cat
/// picker, HO-SHP-001 §8.9). No handover gives this record's fields; inferred as the minimum a
/// picker needs. Flagged as a gap in docs/handover/CONTRACTS.md.
/// </summary>
public sealed record AuthorisedCat(Guid CatId, string CatName, Guid AuthorisationId, DateOnly ValidTo);

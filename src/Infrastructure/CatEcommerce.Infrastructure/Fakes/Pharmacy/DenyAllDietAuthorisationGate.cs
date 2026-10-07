using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Fakes.Pharmacy;

/// <summary>
/// FAKE until HO-PHM-001 lands -- replace this registration.
///
/// Fail-closed, by explicit instruction (HO-SHP-001 §8.9/ADR-084, README C-4): "the shipped stub
/// denies everyone." <see cref="CheckAsync"/> always returns <see cref="DietGateOutcome.NoAuthorisation"/>
/// and <see cref="GetAuthorisedCatsAsync"/> always returns no cats, so a prescription-diet product
/// is never purchasable and no cat is ever offered until Pharmacy is real.
/// </summary>
public sealed class DenyAllDietAuthorisationGate : IDietAuthorisationGate
{
    public Task<DietGateResult> CheckAsync(Guid customerId, Guid catId, Guid skuId, CancellationToken ct) =>
        Task.FromResult(new DietGateResult(
            DietGateOutcome.NoAuthorisation,
            AuthorisationId: null,
            ValidTo: null,
            Message: "This food needs a vet's authorisation for your cat. Book a consultation to get one."));

    public Task<IReadOnlyList<AuthorisedCat>> GetAuthorisedCatsAsync(Guid customerId, Guid skuId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AuthorisedCat>>(Array.Empty<AuthorisedCat>());
}

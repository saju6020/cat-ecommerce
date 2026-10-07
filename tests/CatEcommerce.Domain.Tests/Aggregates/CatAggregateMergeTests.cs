using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.SharedDto.Cat;

namespace CatEcommerce.Domain.Tests.Aggregates;

/// <summary>HO-ACC-001 §15, U-I4/U-I5 -- the one Cat change (§8.9).</summary>
public sealed class CatAggregateMergeTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static CatAggregate RegisteredCat(Guid ownerId, bool deceased = false)
    {
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId, "Mittens", "Cat", CatSex.Female, null, DateOfBirthPrecision.Estimated, null, null, Today,
            TestUserContext.Create(), Guid.NewGuid()));

        if (deceased)
            cat.MarkDeceased(new CatDeceasedDto(Today, "Old age", Today, TestUserContext.Create(), Guid.NewGuid()));

        return cat;
    }

    // U-I4
    [Fact]
    public void ReassignOwnerOnCustomerMerge_DeceasedCat_MovesWithoutRequiringAlive()
    {
        var ownerId = Guid.NewGuid();
        var cat = RegisteredCat(ownerId, deceased: true);
        var survivorId = Guid.NewGuid();

        cat.ReassignOwnerOnCustomerMerge(new CatOwnerMergeReassignmentDto(ownerId, survivorId, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(survivorId, cat.OwnerId);
    }

    // U-I5
    [Fact]
    public void ReassignOwnerOnCustomerMerge_WrongOwner_Throws()
    {
        var cat = RegisteredCat(Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => cat.ReassignOwnerOnCustomerMerge(
            new CatOwnerMergeReassignmentDto(Guid.NewGuid(), Guid.NewGuid(), TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal("CatOwnerMismatch", ex.ErrorCode);
    }

    [Fact]
    public void ReassignOwnerOnCustomerMerge_SameSurvivorAsOwner_Throws()
    {
        var ownerId = Guid.NewGuid();
        var cat = RegisteredCat(ownerId);

        var ex = Assert.Throws<DomainRuleViolationException>(() => cat.ReassignOwnerOnCustomerMerge(
            new CatOwnerMergeReassignmentDto(ownerId, ownerId, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal("OwnerUnchanged", ex.ErrorCode);
    }
}

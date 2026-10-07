using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.Tests.Aggregates.Product;

public sealed class SourcingOriginAggregateTests
{
    [Fact]
    public void Create_Valid_NormalisesCountryCode()
    {
        var origin = new SourcingOriginAggregate(Guid.NewGuid());
        origin.Create(new SourcingOriginCreateDto("de", "Sourced from Germany", true, 1, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal("DE", origin.CountryCode);
        Assert.True(origin.IsFeatured);
        Assert.Single(origin.Events);
    }

    [Fact]
    public void Create_UnknownCountryCode_Throws()
    {
        var origin = new SourcingOriginAggregate(Guid.NewGuid());
        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            origin.Create(new SourcingOriginCreateDto("XX", null, false, 0, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.InvalidCountryCode, ex.ErrorCode);
    }

    [Fact]
    public void Update_ChangesNotesAndFeatured()
    {
        var origin = new SourcingOriginAggregate(Guid.NewGuid());
        origin.Create(new SourcingOriginCreateDto("DE", null, false, 0, TestUserContext.Create(), Guid.NewGuid()));

        origin.Update(new SourcingOriginUpdateDto("Updated notes", true, 5, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal("Updated notes", origin.SourcingNotes);
        Assert.True(origin.IsFeatured);
        Assert.Equal(5, origin.SortOrder);
    }
}

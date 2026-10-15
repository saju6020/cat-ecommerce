using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.Domain.Tests.Aggregates.Administration;

/// <summary>FU1 ORD-54 (ADR-217): the DELIVERY tax class carries the delivery tax and has no fallback, so it can never be deactivated.</summary>
public sealed class TaxClassAggregateTests
{
    private static TaxClassAggregate Class(string code)
    {
        var taxClass = new TaxClassAggregate(Guid.NewGuid());
        taxClass.Create(new TaxClassCreateDto(code, "Some class", TestUserContext.Create(), Guid.NewGuid()));
        return taxClass;
    }

    private static TaxClassUpdateDto Update(bool active) => new("Renamed", active, TestUserContext.Create(), Guid.NewGuid());

    [Fact]
    public void TheDeliveryClass_CannotBeDeactivated_ButCanBeRenamed()
    {
        var delivery = Class(DeliveryTaxFacts.ClassCode);

        var ex = Assert.Throws<DomainRuleViolationException>(() => delivery.Update(Update(active: false)));
        Assert.Equal(BusinessViolationCodes.DeliveryTaxClassRequired, ex.ErrorCode);
        Assert.True(delivery.IsActive);

        delivery.Update(Update(active: true));
        Assert.Equal("Renamed", delivery.Name);
    }

    [Fact]
    public void AnyOtherClass_CanBeDeactivated()
    {
        var other = Class("ZERO");
        other.Update(Update(active: false));
        Assert.False(other.IsActive);
    }
}

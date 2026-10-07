using CatEcommerce.Domain.Aggregates.Vaccination;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;

namespace CatEcommerce.Domain.Tests.Aggregates;

public sealed class VaccineAggregateTests
{
    [Fact]
    public void Register_Valid_SetsPropertiesAndRaisesEvent()
    {
        var vaccine = new VaccineAggregate(Guid.NewGuid());

        vaccine.Register(new VaccineRegistrationDto("RABIES", "Rabies", true, 12, null,
            TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal("RABIES", vaccine.Code);
        Assert.True(vaccine.IsActive);
        Assert.Single(vaccine.Events);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void Register_ValidityMonthsOutOfRange_Throws(int validityMonths)
    {
        var vaccine = new VaccineAggregate(Guid.NewGuid());

        Assert.Throws<DomainRuleViolationException>(() => vaccine.Register(
            new VaccineRegistrationDto("RABIES", "Rabies", true, validityMonths, null,
                TestUserContext.Create(), Guid.NewGuid())));
    }
}

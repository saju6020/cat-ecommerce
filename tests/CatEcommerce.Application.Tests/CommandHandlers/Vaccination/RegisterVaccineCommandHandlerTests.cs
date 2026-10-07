using CatEcommerce.Application.CommandHandlers.Vaccination;
using CatEcommerce.Application.Commands.Vaccination;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Vaccination;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatEcommerce.Application.Tests.CommandHandlers.Vaccination;

public sealed class RegisterVaccineCommandHandlerTests
{
    // U30
    [Fact]
    public async Task HandleAsync_ExistingCode_ReturnsVaccineCodeAlreadyExists()
    {
        var repository = new FakeAggregateRootRepository<VaccineAggregate>();
        var existing = new VaccineAggregate(Guid.NewGuid());
        existing.Register(new VaccineRegistrationDto("RABIES", "Rabies", true, 12, null,
            TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(existing);

        var handler = new RegisterVaccineCommandHandler(
            NullLogger<RegisterVaccineCommandHandler>.Instance, repository);

        var response = await handler.HandleAsync(new RegisterVaccineCommand
        {
            VaccineId = Guid.NewGuid(),
            Code = "RABIES",
            Name = "Rabies (duplicate)",
            IsCore = true,
            ValidityMonths = 12,
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.VaccineCodeAlreadyExists,
            response.ValidationResult!.Errors[0].ErrorCode);
    }

    // U31
    [Fact]
    public async Task HandleAsync_NewCode_Succeeds()
    {
        var repository = new FakeAggregateRootRepository<VaccineAggregate>();
        var handler = new RegisterVaccineCommandHandler(
            NullLogger<RegisterVaccineCommandHandler>.Instance, repository);

        var response = await handler.HandleAsync(new RegisterVaccineCommand
        {
            VaccineId = Guid.NewGuid(),
            Code = "FVRCP",
            Name = "Feline Viral Rhinotracheitis, Calicivirus, Panleukopenia",
            IsCore = true,
            ValidityMonths = 12,
            UserContext = TestUserContext.Create(),
            CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Single(repository.Saved);
    }
}

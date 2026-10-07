using CatEcommerce.Application.CommandHandlers.Account;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Account;

/// <summary>HO-ACC-001 §15 -- Staff & authorization (F) / ADR-009.</summary>
public sealed class CreateStaffAccountCommandHandlerTests
{
    // U-F9
    [Fact]
    public async Task HandleAsync_CallerWithoutAdministratorRole_ReturnsActionNotPermitted()
    {
        var repository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var handler = new CreateStaffAccountCommandHandler(NullLogger<CreateStaffAccountCommandHandler>.Instance, repository);

        var nonAdminContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Nurse"] };

        var response = await handler.HandleAsync(new CreateStaffAccountCommand
        {
            StaffAccountId = Guid.NewGuid(), FullName = "New Vet", MobileNumber = "01712345678", RoleIds = ["Vet"],
            UserContext = nonAdminContext, CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.ActionNotPermitted);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task HandleAsync_CallerWithAdministratorRole_Succeeds()
    {
        var repository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var handler = new CreateStaffAccountCommandHandler(NullLogger<CreateStaffAccountCommandHandler>.Instance, repository);

        var adminContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Administrator"] };

        var response = await handler.HandleAsync(new CreateStaffAccountCommand
        {
            StaffAccountId = Guid.NewGuid(), FullName = "New Vet", MobileNumber = "01712345678", RoleIds = ["Vet"],
            UserContext = adminContext, CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task HandleAsync_DuplicateMobileAmongStaff_ReturnsViolation()
    {
        var repository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var existing = new StaffAccountAggregate(Guid.NewGuid());
        existing.Create(new Domain.Models.StaffAccountCreationDto(
            "Existing", CatEcommerce.Domain.ValueObjects.MobileNumber.Create("01712345678"), null, ["Vet"],
            TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(existing);

        var handler = new CreateStaffAccountCommandHandler(NullLogger<CreateStaffAccountCommandHandler>.Instance, repository);
        var adminContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Administrator"] };

        var response = await handler.HandleAsync(new CreateStaffAccountCommand
        {
            StaffAccountId = Guid.NewGuid(), FullName = "New Vet", MobileNumber = "01712345678", RoleIds = ["Nurse"],
            UserContext = adminContext, CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.MobileNumberAlreadyRegistered);
    }
}

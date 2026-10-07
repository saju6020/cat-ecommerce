using CatEcommerce.Application.CommandHandlers.Account;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Account;

/// <summary>HO-ACC-001 §15, U-F8 -- OQ-11: removing the last Administrator is refused.</summary>
public sealed class ChangeStaffRolesCommandHandlerTests
{
    private static readonly UserContext AdminContext = new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Administrator"] };

    private static StaffAccountAggregate CreateStaff(params string[] roles)
    {
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto("Staff", MobileNumber.Create("01712345678"), null, roles, TestUserContext.Create(), Guid.NewGuid()));
        return staff;
    }

    [Fact]
    public async Task HandleAsync_RemovingLastAdministrator_ReturnsViolation()
    {
        var repository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var lastAdmin = CreateStaff("Administrator");
        repository.Seed(lastAdmin);

        var handler = new ChangeStaffRolesCommandHandler(NullLogger<ChangeStaffRolesCommandHandler>.Instance, repository);

        var response = await handler.HandleAsync(new ChangeStaffRolesCommand
        {
            StaffAccountId = lastAdmin.Id, RoleIds = ["Nurse"], UserContext = AdminContext, CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.LastAdministratorCannotBeRemoved);
    }

    [Fact]
    public async Task HandleAsync_RemovingAdministratorWhenAnotherExists_Succeeds()
    {
        var repository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var admin1 = CreateStaff("Administrator");
        var admin2 = CreateStaff("Administrator");
        repository.Seed(admin1);
        repository.Seed(admin2);

        var handler = new ChangeStaffRolesCommandHandler(NullLogger<ChangeStaffRolesCommandHandler>.Instance, repository);

        var response = await handler.HandleAsync(new ChangeStaffRolesCommand
        {
            StaffAccountId = admin1.Id, RoleIds = ["Nurse"], UserContext = AdminContext, CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
    }
}

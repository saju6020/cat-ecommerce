using CatEcommerce.Application.CommandHandlers.Account;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Account;

/// <summary>Owner, 2026-10-19: an administrator sets a staff member's password and shares it with them.</summary>
public sealed class SetStaffPasswordCommandHandlerTests
{
    private static readonly UserContext Admin = new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Administrator"] };

    private static StaffAccountAggregate ExistingStaff(FakeAggregateRootRepository<StaffAccountAggregate> repository)
    {
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new Domain.Models.StaffAccountCreationDto(
            "Existing Vet", CatEcommerce.Domain.ValueObjects.MobileNumber.Create("01712345678"), null, ["Vet"],
            TestUserContext.Create(), Guid.NewGuid()));
        repository.Seed(staff);
        return staff;
    }

    private static SetStaffPasswordCommandHandler Handler(FakeAggregateRootRepository<StaffAccountAggregate> repository,
        FakePasswordHasher? hasher = null, bool breached = false) =>
        new(NullLogger<SetStaffPasswordCommandHandler>.Instance, repository, hasher ?? new FakePasswordHasher(), new FakeBreachedPasswordChecker(breached));

    [Fact]
    public async Task An_administrator_sets_the_password_and_the_old_sessions_end()
    {
        var repository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var staff = ExistingStaff(repository);
        var stampBefore = staff.SecurityStamp;
        var hasher = new FakePasswordHasher();

        var response = await Handler(repository, hasher).HandleAsync(new SetStaffPasswordCommand
        {
            StaffAccountId = staff.Id, NewPassword = "a-long-shared-password", UserContext = Admin, CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.True(hasher.Verify("a-long-shared-password", staff.PasswordCredential!));
        Assert.NotEqual(stampBefore, staff.SecurityStamp);
    }

    [Fact]
    public async Task Someone_who_is_not_an_administrator_is_refused()
    {
        var repository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var staff = ExistingStaff(repository);

        var response = await Handler(repository).HandleAsync(new SetStaffPasswordCommand
        {
            StaffAccountId = staff.Id, NewPassword = "a-long-shared-password",
            UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Reception"] }, CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.ActionNotPermitted);
        Assert.Null(staff.PasswordCredential);
    }

    [Fact]
    public async Task A_breached_password_is_refused()
    {
        var repository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var staff = ExistingStaff(repository);

        var response = await Handler(repository, breached: true).HandleAsync(new SetStaffPasswordCommand
        {
            StaffAccountId = staff.Id, NewPassword = "password123456", UserContext = Admin, CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.BreachedPassword);
        Assert.Null(staff.PasswordCredential);
    }

    [Fact]
    public async Task An_unknown_staff_account_is_reported_as_not_found()
    {
        var response = await Handler(new FakeAggregateRootRepository<StaffAccountAggregate>()).HandleAsync(new SetStaffPasswordCommand
        {
            StaffAccountId = Guid.NewGuid(), NewPassword = "a-long-shared-password", UserContext = Admin, CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.StaffAccountNotFound);
    }
}

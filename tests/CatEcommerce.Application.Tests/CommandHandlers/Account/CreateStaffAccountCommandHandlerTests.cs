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
        var handler = new CreateStaffAccountCommandHandler(NullLogger<CreateStaffAccountCommandHandler>.Instance, repository, new FakePasswordHasher(), new FakeBreachedPasswordChecker());

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
        var handler = new CreateStaffAccountCommandHandler(NullLogger<CreateStaffAccountCommandHandler>.Instance, repository, new FakePasswordHasher(), new FakeBreachedPasswordChecker());

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

        var handler = new CreateStaffAccountCommandHandler(NullLogger<CreateStaffAccountCommandHandler>.Instance, repository, new FakePasswordHasher(), new FakeBreachedPasswordChecker());
        var adminContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Administrator"] };

        var response = await handler.HandleAsync(new CreateStaffAccountCommand
        {
            StaffAccountId = Guid.NewGuid(), FullName = "New Vet", MobileNumber = "01712345678", RoleIds = ["Nurse"],
            UserContext = adminContext, CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.MobileNumberAlreadyRegistered);
    }

    // Owner, 2026-10-19: the administrator may give a starting password, hashed like any other.
    [Fact]
    public async Task HandleAsync_WithInitialPassword_TheNewStaffMemberCanSignInWithIt()
    {
        var repository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var hasher = new FakePasswordHasher();
        var handler = new CreateStaffAccountCommandHandler(NullLogger<CreateStaffAccountCommandHandler>.Instance, repository, hasher, new FakeBreachedPasswordChecker());
        var adminContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Administrator"] };

        var response = await handler.HandleAsync(new CreateStaffAccountCommand
        {
            StaffAccountId = Guid.NewGuid(), FullName = "New Vet", MobileNumber = "01712345678", RoleIds = ["Vet"],
            InitialPassword = "a-long-shared-password", UserContext = adminContext, CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        var saved = Assert.Single(repository.Saved);
        Assert.NotNull(saved.PasswordCredential);
        Assert.True(hasher.Verify("a-long-shared-password", saved.PasswordCredential!));
    }

    [Fact]
    public async Task HandleAsync_WithABreachedInitialPassword_IsRefusedAndNothingIsSaved()
    {
        var repository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var handler = new CreateStaffAccountCommandHandler(NullLogger<CreateStaffAccountCommandHandler>.Instance, repository, new FakePasswordHasher(), new FakeBreachedPasswordChecker(isBreached: true));
        var adminContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Administrator"] };

        var response = await handler.HandleAsync(new CreateStaffAccountCommand
        {
            StaffAccountId = Guid.NewGuid(), FullName = "New Vet", MobileNumber = "01712345678", RoleIds = ["Vet"],
            InitialPassword = "password123456", UserContext = adminContext, CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.BreachedPassword);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task HandleAsync_WithoutAPassword_StillCreatesTheAccountWithNoPasswordSet()
    {
        var repository = new FakeAggregateRootRepository<StaffAccountAggregate>();
        var handler = new CreateStaffAccountCommandHandler(NullLogger<CreateStaffAccountCommandHandler>.Instance, repository, new FakePasswordHasher(), new FakeBreachedPasswordChecker());
        var adminContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Administrator"] };

        var response = await handler.HandleAsync(new CreateStaffAccountCommand
        {
            StaffAccountId = Guid.NewGuid(), FullName = "New Vet", MobileNumber = "01712345678", RoleIds = ["Vet"],
            UserContext = adminContext, CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Null(Assert.Single(repository.Saved).PasswordCredential);
    }
}

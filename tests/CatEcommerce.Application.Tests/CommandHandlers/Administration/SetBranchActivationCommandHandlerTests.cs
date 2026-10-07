using CatEcommerce.Application.CommandHandlers.Administration;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Administration;

/// <summary>HO-ADM-001 §15/§16 I-B4 -- "deactivate the only active branch" needs global state
/// (every other active branch already deactivated) that a shared, concurrently-tested database
/// cannot safely reproduce (§16.1). Covered here instead with a fake repository holding exactly
/// the branches this test creates.</summary>
public sealed class SetBranchActivationCommandHandlerTests
{
    private static UserContext AdminContext() => new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Administrator"] };

    private static BranchAggregate CreateActiveBranch(string code)
    {
        var branch = new BranchAggregate(Guid.NewGuid());
        branch.Create(new BranchCreateDto(code, "Branch " + code, "Asia/Dhaka", null, null, null, AdminContext(), Guid.NewGuid()));
        return branch;
    }

    [Fact]
    public async Task Deactivating_TheOnlyActiveBranch_ReturnsLastActiveBranchCannotBeDeactivated()
    {
        var repository = new FakeAggregateRootRepository<BranchAggregate>();
        var onlyBranch = CreateActiveBranch("ONLY");
        repository.Seed(onlyBranch);

        var handler = new SetBranchActivationCommandHandler(repository);
        var response = await handler.HandleAsync(new SetBranchActivationCommand
        {
            BranchId = onlyBranch.Id, IsActive = false, UserContext = AdminContext(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.LastActiveBranchCannotBeDeactivated);
        Assert.True(onlyBranch.IsActive);
    }

    [Fact]
    public async Task Deactivating_OneOfTwoActiveBranches_Succeeds()
    {
        var repository = new FakeAggregateRootRepository<BranchAggregate>();
        var branchA = CreateActiveBranch("BR-A");
        var branchB = CreateActiveBranch("BR-B");
        repository.Seed(branchA);
        repository.Seed(branchB);

        var handler = new SetBranchActivationCommandHandler(repository);
        var response = await handler.HandleAsync(new SetBranchActivationCommand
        {
            BranchId = branchA.Id, IsActive = false, UserContext = AdminContext(), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.False(branchA.IsActive);
        Assert.True(branchB.IsActive);
    }
}

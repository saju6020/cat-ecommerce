using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Application.Tests.CommandHandlers.Billing;

/// <summary>HO-BIL-001 §11.4 C1 -- the seller profile is load-or-create by branch id; a missing branch is a
/// returned violation, never a throw (ADR-014).</summary>
public sealed class SetBillingProfileCommandHandlerTests
{
    private static SetBillingProfileCommand Command(Guid branchId, string legalName = "PurrCare Ltd", IReadOnlyList<BankAccountInput>? accounts = null) => new()
    {
        BranchId = branchId, LegalName = legalName, TaxRegistrationNumber = "BIN-001", AddressLines = ["1 Road", "Dhaka"],
        Phone = "01700000000", Email = "billing@example.test", InvoiceFooter = "Thank you",
        BankAccounts = accounts ?? [new BankAccountInput("Bank", "PurrCare", "123456", null, null)],
        UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
    };

    private static (SetBillingProfileCommandHandler Handler, FakeAggregateRootRepository<BillingProfileAggregate> Profiles, Guid BranchId) Arrange(bool branchExists = true)
    {
        var branches = new FakeAggregateRootRepository<BranchAggregate>();
        var branchId = Guid.NewGuid();
        if (branchExists) branches.Seed(new BranchAggregate(branchId));

        var profiles = new FakeAggregateRootRepository<BillingProfileAggregate>();
        return (new SetBillingProfileCommandHandler(branches, profiles), profiles, branchId);
    }

    [Fact]
    public async Task AMissingBranch_IsReturnedAsBranchNotFound()
    {
        var (handler, profiles, branchId) = Arrange(branchExists: false);

        var response = await handler.HandleAsync(Command(branchId));

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.BranchNotFound);
        Assert.Empty(profiles.Saved);
    }

    [Fact]
    public async Task TheFirstSet_CreatesTheProfileWithTheBranchIdAsItsId()
    {
        var (handler, profiles, branchId) = Arrange();

        var response = await handler.HandleAsync(Command(branchId));

        Assert.True(response.IsSuccess);
        var saved = Assert.Single(profiles.Saved);
        Assert.Equal(branchId, saved.Id);
        Assert.Equal("PurrCare Ltd", saved.LegalName);
        Assert.Single(saved.BankAccounts);
        Assert.Empty(profiles.Updated);
    }

    [Fact]
    public async Task ASecondSet_UpdatesTheSameProfile_ReplacingTheBankAccounts()
    {
        var (handler, profiles, branchId) = Arrange();
        await handler.HandleAsync(Command(branchId));

        var response = await handler.HandleAsync(Command(branchId, "PurrCare Ltd (new)",
            [new BankAccountInput("Other", "PurrCare", "999", "Gulshan", "0123"), new BankAccountInput("Third", "PurrCare", "888", null, null)]));

        Assert.True(response.IsSuccess);
        var updated = Assert.Single(profiles.Updated);
        Assert.Equal("PurrCare Ltd (new)", updated.LegalName);
        Assert.Equal(2, updated.BankAccounts.Count);
        Assert.Single(profiles.Saved);
    }

    [Fact]
    public async Task ABusinessRuleViolation_IsReturned_NotThrown()
    {
        var (handler, profiles, branchId) = Arrange();
        var six = Enumerable.Range(0, 6).Select(i => new BankAccountInput("Bank", "Acc", "N" + i, null, null)).ToList();

        var response = await handler.HandleAsync(Command(branchId, accounts: six));

        Assert.False(response.IsSuccess);
        Assert.Empty(profiles.Saved);
    }

    [Fact]
    public void TheValidator_RefusesABlankLegalName_AndSixBankAccounts_AndAControlCharacter()
    {
        var validator = new SetBillingProfileCommandValidator();
        var branchId = Guid.NewGuid();

        Assert.True(validator.Validate(Command(branchId)).IsValid);
        Assert.False(validator.Validate(Command(branchId, legalName: "")).IsValid);
        Assert.False(validator.Validate(Command(branchId, legalName: "Bad\u0007Name")).IsValid);
        Assert.False(validator.Validate(Command(branchId, accounts: Enumerable.Range(0, 6).Select(i => new BankAccountInput("B", "A", "N" + i, null, null)).ToList())).IsValid);
    }
}

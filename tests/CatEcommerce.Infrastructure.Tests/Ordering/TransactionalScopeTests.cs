using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Infrastructure.Ordering;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.Infrastructure.Tests.Ordering;

/// <summary>
/// HO-ORD-001 CODE_REVIEW backlog 1 -- the commit-time stock race translation, deterministically. The 23514 check violations Postgres raises when the last unit went to
/// another caller (or the batch expired / was recalled between plan and commit) become the business refusal <c>StockNoLongerAvailable</c>; every other exception passes
/// through unchanged. No database: Billing's published scope is a stand-in that runs the work and lets the exception out, exactly as the real one does.
/// </summary>
public sealed class TransactionalScopeTests
{
    private sealed class PassThroughBillingScope : IBillingTransactionScope
    {
        public Func<Task>? OnFlush { get; init; }
        public Task<T> RunAsync<T>(Func<Task<T>> work, Func<T, bool> commitWhen, CancellationToken ct = default) => work();
        public Task FlushAsync(CancellationToken ct = default) => OnFlush?.Invoke() ?? Task.CompletedTask;
    }

    private static DbUpdateException Violation(string sqlState, string? constraint) =>
        new("An error occurred while saving the entity changes.", new PostgresException("violation", "ERROR", "ERROR", sqlState, constraintName: constraint));

    [Theory]
    [InlineData("ck_stock_position_balance")]
    [InlineData("tg_stock_batch_expired")]
    [InlineData("tg_stock_batch_not_usable")]
    public async Task ACheckViolationOnAStockRaceConstraint_BecomesStockNoLongerAvailable(string constraint)
    {
        var scope = new TransactionalScope(new PassThroughBillingScope());

        var ex = await Assert.ThrowsAsync<DomainRuleViolationException>(() => scope.RunAsync<int>(() => throw Violation(PostgresErrorCodes.CheckViolation, constraint), _ => true));

        Assert.Equal(BusinessViolationCodes.StockNoLongerAvailable, ex.ErrorCode);
    }

    [Fact]
    public async Task TheSameTranslationAppliesToAFlush()
    {
        var scope = new TransactionalScope(new PassThroughBillingScope { OnFlush = () => throw Violation(PostgresErrorCodes.CheckViolation, "ck_stock_position_balance") });

        var ex = await Assert.ThrowsAsync<DomainRuleViolationException>(() => scope.FlushAsync());

        Assert.Equal(BusinessViolationCodes.StockNoLongerAvailable, ex.ErrorCode);
    }

    [Theory]
    [InlineData(PostgresErrorCodes.CheckViolation, "ck_some_other_constraint")]
    [InlineData(PostgresErrorCodes.UniqueViolation, "ck_stock_position_balance")]
    [InlineData(PostgresErrorCodes.CheckViolation, null)]
    public async Task AnyOtherDatabaseFailure_PassesThroughUnchanged(string sqlState, string? constraint)
    {
        var scope = new TransactionalScope(new PassThroughBillingScope());
        var original = Violation(sqlState, constraint);

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => scope.RunAsync<int>(() => throw original, _ => true));

        Assert.Same(original, thrown);
    }

    [Fact]
    public async Task ASuccess_IsReturnedAsIs()
    {
        var scope = new TransactionalScope(new PassThroughBillingScope());

        Assert.Equal(7, await scope.RunAsync(() => Task.FromResult(7), _ => true));
    }
}

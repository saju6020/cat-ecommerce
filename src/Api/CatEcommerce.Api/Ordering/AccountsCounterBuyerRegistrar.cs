using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Ordering;

/// <summary>
/// HO-ORD-001 ADR-216 (amended 2026-10-10, backlog ORD 42/43/47; FU1 D1/D2) -- <see cref="ICounterBuyerRegistrar"/> over Accounts' published <c>RegisterWalkInCustomerCommand</c>.
/// Runs before the sale's transaction opens, so Accounts' own save is a short transaction of its own and a lost race on <c>ix_customer_account_mobile_number</c> (23505)
/// aborts only that save: the failed attempt's tracked rows are discarded and the buyer the other cashier registered is found and used.
/// </summary>
public sealed class AccountsCounterBuyerRegistrar(
    IInMemoryDispatcher dispatcher, ICounterCustomerFinder finder, CatEcommerceDbContext db) : ICounterBuyerRegistrar
{
    private const string MobileIndex = "ix_customer_account_mobile_number";

    public async Task<CounterBuyerResolution> FindOrRegisterAsync(string normalisedMobile, string fullName, UserContext userContext, Guid correlationId, CancellationToken ct)
    {
        if (await finder.FindActiveByPrimaryMobileAsync(normalisedMobile, ct) is { } existing) return CounterBuyerResolution.Found(existing);

        var customerId = Guid.NewGuid();
        var name = fullName.Trim();
        string? accountsRefusal = null;
        var alreadyTracked = db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
        try
        {
            var response = await dispatcher.SendAsync(new RegisterWalkInCustomerCommand
            {
                CustomerId = customerId, FullName = name, MobileNumber = normalisedMobile, UserContext = userContext, CorrelationId = correlationId, ValidateCommand = true,
            });

            if (response.IsSuccess) return CounterBuyerResolution.Found(new CustomerContactFacts(customerId, name, normalisedMobile, IsActive: true));
            accountsRefusal = response.ValidationResult?.Errors.FirstOrDefault()?.ErrorMessage;
        }
        catch (Exception ex) when (IsMobileCollision(ex))
        {
            DiscardFailedAttempt(alreadyTracked);
        }

        // Raced: another cashier registered this mobile between our lookup and our insert.
        if (await finder.FindActiveByPrimaryMobileAsync(normalisedMobile, ct) is { } raced) return CounterBuyerResolution.Found(raced);

        // The mobile is held, but not by an Active account (Merged or Anonymised): the cashier's problem, in the cashier's words -- not the customer's reset offer.
        if (await db.CustomerAccounts.AsNoTracking().AnyAsync(c => c.MobileNumber!.Value == normalisedMobile, ct))
            return CounterBuyerResolution.Refused(BusinessViolationCodes.AccountNotActive, OrderMessages.CounterMobileHeldByInactiveAccount, "CustomerMobile");

        return CounterBuyerResolution.Refused(
            BusinessViolationCodes.CustomerNotFound, accountsRefusal ?? "The customer could not be registered.", "CustomerName");
    }

    private static bool IsMobileCollision(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: MobileIndex }) return true;

        return false;
    }

    /// <summary>The failed save left its rows tracked as Added (the walk-in, its audit and outbox rows); only those -- not an entry that was Added before the attempt -- are detached (backlog ORD 47).</summary>
    private void DiscardFailedAttempt(HashSet<object> addedBefore)
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added && !addedBefore.Contains(e.Entity)).ToList())
            entry.State = EntityState.Detached;
    }
}

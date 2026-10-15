using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>The buyer a counter sale resolved to, or the cashier-facing refusal when none could be.</summary>
public sealed record CounterBuyerResolution(CustomerContactFacts? Buyer, string? RefusalCode, string? RefusalMessage, string? RefusalProperty)
{
    public static CounterBuyerResolution Found(CustomerContactFacts buyer) => new(buyer, null, null, null);

    public static CounterBuyerResolution Refused(string code, string message, string property) => new(null, code, message, property);
}

/// <summary>
/// HO-ORD-001 ADR-216 (amended 2026-10-10, backlog ORD 42/43) -- finds or registers the buyer of a counter sale <b>in its own short transaction, before the sale's
/// transaction opens</b>. Finding by mobile comes first; otherwise the walk-in is registered through Accounts' <c>RegisterWalkInCustomerCommand</c>, and when a concurrent
/// cashier registered the same mobile a moment earlier (the unique index answers) the other buyer is found and used -- so every concurrent sale proceeds against one account.
/// A mobile held by a Merged or Anonymised account is a clear refusal, never the customer-facing "log in or reset your password" message.
/// </summary>
public interface ICounterBuyerRegistrar
{
    Task<CounterBuyerResolution> FindOrRegisterAsync(string normalisedMobile, string fullName, UserContext userContext, Guid correlationId, CancellationToken ct);
}

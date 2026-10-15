using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-XCT-001 section 8.3. Opens the ledger of one customer's erasure request. The ledger id IS the customer account id (1:1), so a
/// second anonymisation event finds the existing ledger.</summary>
public sealed record ErasureOpenDto(
    DateTimeOffset RequestedOn,
    ErasureRequestSource Source,
    IReadOnlyList<string> ContextKeys,
    UserContext UserContext,
    Guid CorrelationId);

/// <summary>Adds a scope for every registered context key the ledger does not have yet (a context added after the request was opened).</summary>
public sealed record ErasureAddScopesDto(
    IReadOnlyList<string> ContextKeys,
    UserContext UserContext,
    Guid CorrelationId);

/// <summary>One eraser's result for one scope. <paramref name="Today"/> is the current date used to refuse a hold that is not in the future.</summary>
public sealed record ErasureScopeOutcomeDto(
    string ContextKey,
    ErasureOutcome Outcome,
    DateTimeOffset Now,
    DateOnly Today,
    UserContext UserContext,
    Guid CorrelationId);

using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Operations;

/// <summary>HO-XCT-001 section 11.1 C1 -- opens (or tops up) the erasure ledger of one anonymised account. Sent by the ledger-opening event
/// handler and by the retention worker's self-heal. Idempotent: a ledger that exists only gains scopes for contexts added since.</summary>
public sealed class OpenErasureRequestCommand : Command
{
    public required Guid CustomerAccountId { get; init; }
    public required DateTimeOffset RequestedOn { get; init; }

    /// <summary>An <c>ErasureRequestSource</c> display name.</summary>
    public required string Source { get; init; }
}

/// <summary>C2 -- the worker's execution of one due scope: calls the context's eraser and records the outcome on the ledger.</summary>
public sealed class ExecuteErasureScopeCommand : Command
{
    public required Guid CustomerAccountId { get; init; }
    public required string ContextKey { get; init; }
}

/// <summary>C3 -- an administrator takes a deletion request on behalf of a customer (phone, in person, email, letter).</summary>
public sealed class EraseCustomerOnRequestCommand : Command
{
    public required Guid CustomerAccountId { get; init; }

    /// <summary>An <c>ErasureRequestChannel</c> display name.</summary>
    public required string Channel { get; init; }

    /// <summary>How the operator verified who was asking: an <c>ErasureIdentityCheck</c> display name (a fixed code, never free text, because it is written to the audit trail).</summary>
    public required string IdentityCheck { get; init; }
}

/// <summary>C4 -- after a restore to a point earlier than the latest WAL, re-erases the customers who had asked to be forgotten since that point
/// (ADR-314). The ids come from the erasure journal exported with every backup.</summary>
public sealed class ReapplyErasuresAfterRestoreCommand : Command
{
    public required IReadOnlyList<Guid> CustomerAccountIds { get; init; }
    public required string RestoreReference { get; init; }
}

/// <summary>Result of C3.</summary>
public sealed record ErasureOnRequestResult(bool IsReapplication);

/// <summary>Result of C4. <c>Failed</c> counts accounts whose anonymisation threw (recorded, never stopping the rest).</summary>
public sealed record ReapplyErasuresResult(int Processed, int AlreadyErased, int NotFound, int Merged, int Failed);

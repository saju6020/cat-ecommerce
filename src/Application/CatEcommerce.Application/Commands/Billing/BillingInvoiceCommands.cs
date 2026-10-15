using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Billing;

/// <summary>HO-BIL-001 §11.1 C5 -- a draft invoice composed from the unbilled active charges of one customer at the
/// active branch (any business line -- US-ORD-17's server half).</summary>
public sealed class CreateDraftInvoiceCommand : Command
{
    public required Guid InvoiceId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid BranchId { get; init; }
    public required IReadOnlyList<Guid> ChargeIds { get; init; }
    public string? Notes { get; init; }
}

/// <summary>C6 -- replaces the charges of a draft. <see cref="ExpectedVersion"/> guards a stale screen (<c>409</c>).</summary>
public sealed class SetDraftInvoiceChargesCommand : Command
{
    public required Guid InvoiceId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
    public required IReadOnlyList<Guid> ChargeIds { get; init; }
}

/// <summary>C6 -- discards a draft; its charges return to unbilled.</summary>
public sealed class DiscardDraftInvoiceCommand : Command
{
    public required Guid InvoiceId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>C8 -- issue a draft: number it (gapless, ADR-179), freeze rate/tax/totals/seller/bill-to, enqueue the message.</summary>
public sealed class IssueInvoiceCommand : Command
{
    public required Guid InvoiceId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
    public string? IdempotencyKey { get; init; }
}

/// <summary>Exactly one of <see cref="ChargeIds"/>, <see cref="Source"/> or <see cref="BillingGroup"/> (§8.13).</summary>
public sealed record InvoiceChargeSelector(
    IReadOnlyList<Guid>? ChargeIds = null, SourceChargeSelector? Source = null, BillingGroupChargeSelector? BillingGroup = null)
{
    public int FormCount => (ChargeIds is not null ? 1 : 0) + (Source is not null ? 1 : 0) + (BillingGroup is not null ? 1 : 0);
}

/// <summary>Every unbilled charge of a source (an order, an appointment) -- <c>Kind</c> is a <c>ChargeSourceKind</c> name.</summary>
public sealed record SourceChargeSelector(string Kind, Guid Id);

/// <summary>Every unbilled charge of a billing group (a boarding stay) up to <c>ServiceDateTo</c> -- a cycle invoice (US-BRD-20).</summary>
public sealed record BillingGroupChargeSelector(string Kind, Guid Id, DateOnly? ServiceDateTo = null);

/// <summary>C9 -- the published contract of §8.13: select the charges, create the draft and issue it <b>in the same
/// save</b>. <see cref="BranchId"/> is the caller's active branch on the staff path, the producer's branch otherwise.</summary>
public sealed class IssueInvoiceFromChargesCommand : Command
{
    public required Guid InvoiceId { get; init; }
    public required Guid CustomerId { get; init; }
    public Guid? BranchId { get; init; }
    public required InvoiceChargeSelector Selector { get; init; }
    public string? IdempotencyKey { get; init; }

    /// <summary>Additive (HO-ORD-001-FU1 ORD-56; <c>CONTRACTS.md</c> #29). In-process only, never bound from HTTP: the tax class whose rate is read as of <see cref="PinnedTaxRateAsOf"/> instead of now --
    /// Ordering pins the DELIVERY class to the order's placement instant so a held transfer is invoiced at the delivery tax the customer was quoted.</summary>
    public Guid? PinnedTaxClassId { get; init; }

    public DateTimeOffset? PinnedTaxRateAsOf { get; init; }
}

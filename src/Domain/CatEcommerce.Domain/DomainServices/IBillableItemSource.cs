using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One billable thing a source area (an appointment, later a visit, dispense or surgery) tells
/// Billing about. <see cref="TaxClassId"/> null means the source could not name one -- the item is
/// <b>skipped and reported</b> (<c>TaxClassNotFound</c>), never guessed (§8.5).</summary>
public sealed record BillableSourceItem(
    Guid CustomerId, Guid BranchId, Guid? CatId, ItemKind ItemKind, string Description, decimal Quantity,
    Money UnitPrice, Money LineDiscount, Guid? TaxClassId, DateOnly ServiceDate, string SourceLineRef,
    BillingGroupKind? BillingGroupKind, Guid? BillingGroupId);

/// <summary><see cref="IsBillable"/> false means the source exists but is not in a billable state yet
/// (e.g. an appointment not <c>Completed</c>); <see cref="Reason"/> says why. Items are meaningful only
/// when billable. A source that does not exist at all is <see cref="NotFound"/>. <see cref="Skipped"/> are the
/// reason codes of things the source found but could not turn into an item (a medicine with no price recorded):
/// the capture reports them beside its own skips and never invents the missing value.</summary>
public sealed record BillableSourceResult(bool Exists, bool IsBillable, string? Reason, IReadOnlyList<BillableSourceItem> Items, IReadOnlyList<string>? SkippedReasons = null, Guid? SourceBranchId = null)
{
    /// <summary>The branch the source belongs to, set by every source that exists. The capture checks it BEFORE it says anything else about the source, so another
    /// branch's source answers <c>SourceNotFound</c> and never reveals its state (SECURITY gate, Phase J).</summary>
    public Guid? BranchId => SourceBranchId;

    public BillableSourceResult InBranch(Guid branchId) => this with { SourceBranchId = branchId };

    public IReadOnlyList<string> Skipped => SkippedReasons ?? [];

    public static BillableSourceResult NotFound { get; } = new(false, false, "SourceNotFound", []);

    public static BillableSourceResult NotBillable(string reason) => new(true, false, reason, []);

    public static BillableSourceResult Billable(IReadOnlyList<BillableSourceItem> items) => new(true, true, null, items);

    public static BillableSourceResult Billable(IReadOnlyList<BillableSourceItem> items, IReadOnlyList<string> skipped) => new(true, true, null, items, skipped);
}

/// <summary>
/// HO-BIL-001 §8.5 / §12.2 (ADR-178) -- the pull path: Billing implements one source per
/// <see cref="ChargeSourceKind"/> it can capture. Phase B built the appointment source; Phase J (owner decisions 2026-10-14) adds the
/// visit source (read through EMR's <see cref="IVisitBillingReader"/>), the dispense source and the surgery source.
/// </summary>
public interface IBillableItemSource
{
    ChargeSourceKind Kind { get; }

    Task<BillableSourceResult> GetItemsAsync(Guid sourceId, CancellationToken ct = default);
}

/// <summary>Resolves the item source registered for a source kind (§12.7 -- keyed by
/// <see cref="ChargeSourceKind"/>). Null when Billing cannot capture that kind.</summary>
public interface IBillableItemSourceResolver
{
    IBillableItemSource? Resolve(ChargeSourceKind kind);
}

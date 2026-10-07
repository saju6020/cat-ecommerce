using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// The reference document a stock movement traces back to -- HO-IMP-001 §8.8's <c>IStockLedger</c>
/// (ADR-103/ADR-104). Field names and order are the owner's (HO-IMP-001 §8.8's own table row:
/// "<c>ReferenceKind</c>, <c>ReferenceId</c>, <c>ReferenceLineId?</c>, <c>ReferenceLabel?</c>"),
/// which differ from several consumers' inline code samples (HO-PHM-001 uses <c>Kind</c>/<c>Label</c>)
/// -- recorded as a disagreement in docs/handover/CONTRACTS.md, resolved in the owner's favour.
/// </summary>
public sealed record StockReference(
    StockReferenceKind ReferenceKind,
    Guid ReferenceId,
    Guid? ReferenceLineId,
    string? ReferenceLabel);

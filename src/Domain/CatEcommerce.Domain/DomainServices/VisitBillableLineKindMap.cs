using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BIL-001 Phase J (owner's architecture call 2026-10-14, backlog EMR 43) -- the one explicit translation from EMR's line kinds to Billing's
/// <see cref="ItemKind"/>. It lives in Billing's side of the seam: EMR speaks Procedure / Medicine / Consumable (and a diagnostic test), Billing speaks
/// Service / Medicine / Product. Exhaustive on purpose: a kind added to EMR without a case here throws instead of being billed under a guess.
/// <list type="bullet">
/// <item>Procedure -> Service: the vet's work, priced from the clinic service list.</item>
/// <item>Medicine -> Medicine: a medicine given during the visit.</item>
/// <item>Consumable -> Product: a stock item used (syringes, dressings), sold at its SKU price.</item>
/// <item>Diagnostic -> Service: a test the clinic performs or sends out; it has no SKU.</item>
/// </list>
/// </summary>
public static class VisitBillableLineKindMap
{
    public static ItemKind ItemKindFor(VisitBillableLineKind kind)
    {
        if (kind == VisitBillableLineKind.Procedure) return ItemKind.Service;
        if (kind == VisitBillableLineKind.Medicine) return ItemKind.Medicine;
        if (kind == VisitBillableLineKind.Consumable) return ItemKind.Product;
        if (kind == VisitBillableLineKind.Diagnostic) return ItemKind.Service;
        throw new ArgumentOutOfRangeException(nameof(kind), kind.DisplayName, "This visit line kind has no Billing item kind.");
    }
}

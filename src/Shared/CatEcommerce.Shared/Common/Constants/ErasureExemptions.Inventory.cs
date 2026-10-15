namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-IMP-001 (HO-XCT-001 §5.2 C-ALL-2) -- Inventory stores no customer personal data beyond ids, so it is
/// exempted rather than given an <c>IPersonalDataEraser</c>. The ledger's <c>customer_id</c>/<c>cat_id</c>
/// are immutable references (ADR-098, ADR-104): an anonymised customer is shown as "Anonymised customer"
/// at read time. Supplier contacts are business contacts, not customer data.
/// </summary>
public static partial class ErasureExemptions
{
    private static readonly IReadOnlyList<(string ContextKey, string Reason)> InventoryEntries =
    [
        ("inventory", "Ids only: ledger and reservation entries hold customer and cat ids as immutable recall-trace references, resolved (and shown as 'Anonymised customer') at read time (ADR-104); supplier contacts are business contacts."),
    ];
}

namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.7 / §10.1. The two gapless series per branch and financial year. Named
/// BillingDocumentKind at merge (integrator, 2026-10-07): HO-IMP-001 owns DocumentKind (its stock-document
/// sequences, ADR-114); values unchanged, persisted by Value in billing.document_number_series.</summary>
public sealed class BillingDocumentKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BillingDocumentKind Invoice = new(1, nameof(Invoice));
    public static readonly BillingDocumentKind CreditNote = new(2, nameof(CreditNote));
}

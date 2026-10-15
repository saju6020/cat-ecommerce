namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- §8.5. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class ImportDocumentKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ImportDocumentKind ImportPermit = new(1, nameof(ImportPermit));
    public static readonly ImportDocumentKind HealthCertificate = new(2, nameof(HealthCertificate));
    public static readonly ImportDocumentKind VeterinaryCertificate = new(3, nameof(VeterinaryCertificate));
    public static readonly ImportDocumentKind CustomsDeclaration = new(4, nameof(CustomsDeclaration));
    public static readonly ImportDocumentKind BillOfLading = new(5, nameof(BillOfLading));
    public static readonly ImportDocumentKind CommercialInvoice = new(6, nameof(CommercialInvoice));
    public static readonly ImportDocumentKind PackingList = new(7, nameof(PackingList));
    public static readonly ImportDocumentKind CertificateOfOrigin = new(8, nameof(CertificateOfOrigin));
    public static readonly ImportDocumentKind Other = new(9, nameof(Other));
}

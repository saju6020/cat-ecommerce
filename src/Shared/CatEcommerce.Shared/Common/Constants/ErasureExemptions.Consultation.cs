namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Consultation and Medical Records (HO-EMR-001) is exempted from <c>IPersonalDataEraser</c>
/// registration (HO-XCT-001 §5.2 C-ALL-2). It stores the cat's clinical record (visits, results,
/// addenda, photographs) keyed by cat and staff ids -- no customer id on any visit row (§8.2) -- and,
/// on a consent, the signer's name and signature, which are a Regulatory record retained under
/// NFR-LEG-4 and D-8 and so not erasable (D-11 "subject to D-8"). Files attached to those records are
/// protected from deletion by the database while they are referenced (see the migration).</summary>
public static partial class ErasureExemptions
{
    private static readonly IReadOnlyList<(string ContextKey, string Reason)> ConsultationEntries =
    [
        ("consultation", "Clinical record keyed by cat and staff ids, no customer id; consent name/signature is a Regulatory record retained under NFR-LEG-4 and D-8 (HO-EMR-001 §8.2/§8.9, HO-XCT-001 §8.8)."),
    ];
}

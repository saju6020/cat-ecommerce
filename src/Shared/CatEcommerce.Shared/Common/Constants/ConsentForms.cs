using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-EMR-001 §8.9 -- the HO-ADM-001 policy-document key of each consent form
/// (<c>RequiresAcceptance = false</c>; wording authored by the owner, OQ-15).</summary>
public static class ConsentForms
{
    public const string Surgery = "consent-surgery";
    public const string Anaesthesia = "consent-anaesthesia";
    public const string Euthanasia = "consent-euthanasia";

    public static string KeyFor(ConsentKind kind) =>
        kind == ConsentKind.Surgery ? Surgery
        : kind == ConsentKind.Anaesthesia ? Anaesthesia
        : kind == ConsentKind.Euthanasia ? Euthanasia
        : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown consent kind.");
}

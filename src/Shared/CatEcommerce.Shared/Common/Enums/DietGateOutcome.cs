namespace CatEcommerce.Shared.Common.Enums;

/// <summary>Owned by HO-PHM-001 §8.10/§10.1 (ADR-149). The result of <c>IDietAuthorisationGate.CheckAsync</c>.</summary>
public sealed class DietGateOutcome(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DietGateOutcome NotRequired = new(1, nameof(NotRequired));
    public static readonly DietGateOutcome Authorised = new(2, nameof(Authorised));
    public static readonly DietGateOutcome NoAuthorisation = new(3, nameof(NoAuthorisation));
    public static readonly DietGateOutcome AuthorisationExpired = new(4, nameof(AuthorisationExpired));
    public static readonly DietGateOutcome CatNotOwned = new(5, nameof(CatNotOwned));
    public static readonly DietGateOutcome CatDeceased = new(6, nameof(CatDeceased));
}

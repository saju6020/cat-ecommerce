using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-ACC-001 §8.4. Both session aggregates (<c>StateAt</c>) and the read side call
/// <see cref="Classify"/> so they cannot disagree about what a session's current state is.
/// </summary>
public static class SessionStateRules
{
    /// <summary>Customer session (and the base classification staff sessions build on).</summary>
    public static SessionState Classify(
        bool isEnded, DateTimeOffset expiresOn, DateTimeOffset asOf,
        Guid issuedForSecurityStamp, Guid currentStamp)
    {
        if (isEnded) return SessionState.Ended;
        if (asOf >= expiresOn) return SessionState.Expired;
        if (issuedForSecurityStamp != currentStamp) return SessionState.Revoked;
        return SessionState.Active;
    }

    /// <summary>Staff session -- adds idle-timeout classification once every other check has passed.</summary>
    public static SessionState Classify(
        bool isEnded, DateTimeOffset expiresOn, DateTimeOffset asOf,
        Guid issuedForSecurityStamp, Guid currentStamp,
        DateTimeOffset lastActivityOn, TimeSpan idleTimeout)
    {
        var baseState = Classify(isEnded, expiresOn, asOf, issuedForSecurityStamp, currentStamp);
        if (baseState != SessionState.Active) return baseState;

        return asOf - lastActivityOn >= idleTimeout ? SessionState.IdleExpired : SessionState.Active;
    }
}

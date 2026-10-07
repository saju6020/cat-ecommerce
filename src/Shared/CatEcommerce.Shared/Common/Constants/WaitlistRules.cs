namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-APT-001 §8.11/§10.3.</summary>
public static class WaitlistRules
{
    /// <summary>How many branch/service/date groups a single <c>ClinicWorker</c> offer pass
    /// inspects, per §12.2 -- a defensive cap, not a business rule.</summary>
    public const int MaxOfferGroupsPerPass = 200;

    public const int MaxClosedPerPass = 200;
}

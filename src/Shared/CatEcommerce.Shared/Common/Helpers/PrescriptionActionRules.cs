using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>The named actions of the prescription state machine (§8.4).</summary>
public static class PrescriptionActions
{
    public const string Dispense = "Dispense";
    public const string Cancel = "Cancel";
    public const string Renew = "Renew";
    public const string DeclineRepeat = "DeclineRepeat";
    public const string Expire = "Expire";
    public const string ReverseSupply = "ReverseSupply";
}

/// <summary>Why an owner cannot request a repeat right now -- the same answer the command gives and the portal shows (§12.4).</summary>
public sealed record RepeatEligibility(bool CanRequest, string? ViolationCode, string? Message);

/// <summary>
/// HO-PHM-001 §8.4 / §12.4 -- the status transition table and the allowed-actions computation, shared by the aggregate (which enforces them) and the
/// detail read (which shows them), so the screen never offers a button the command would refuse. Pure.
/// </summary>
public static class PrescriptionActionRules
{
    /// <summary>The statuses an action may start from (§8.4). Anything else is <c>InvalidPrescriptionTransition</c>.</summary>
    public static IReadOnlyList<PrescriptionStatus> AllowedFrom(string action) => action switch
    {
        PrescriptionActions.Dispense => [PrescriptionStatus.Issued, PrescriptionStatus.Dispensed],
        PrescriptionActions.Cancel => [PrescriptionStatus.Issued, PrescriptionStatus.Dispensed],
        PrescriptionActions.Expire => [PrescriptionStatus.Issued, PrescriptionStatus.Dispensed],
        PrescriptionActions.Renew => [PrescriptionStatus.Issued, PrescriptionStatus.Dispensed, PrescriptionStatus.Expired],
        PrescriptionActions.ReverseSupply => [PrescriptionStatus.Dispensed, PrescriptionStatus.Completed],
        PrescriptionActions.DeclineRepeat => [PrescriptionStatus.Issued, PrescriptionStatus.Dispensed],
        _ => []
    };

    public static bool IsAllowed(PrescriptionStatus status, string action) => AllowedFrom(action).Contains(status);

    public static IReadOnlyList<string> AllActions { get; } =
    [
        PrescriptionActions.Dispense, PrescriptionActions.Cancel, PrescriptionActions.Expire, PrescriptionActions.Renew,
        PrescriptionActions.ReverseSupply, PrescriptionActions.DeclineRepeat
    ];

    /// <summary>The repeat rule of §8.8, in the order the aggregate checks it.</summary>
    public static RepeatEligibility EvaluateRepeat(
        bool isRepeatable, PrescriptionStatus status, int suppliesLeft, DateOnly validUntil, bool hasOpenRequest, DateOnly today)
    {
        if (!isRepeatable)
            return new RepeatEligibility(false, BusinessViolationCodes.NotRepeatable, "This prescription cannot be repeated.");

        if (status == PrescriptionStatus.Cancelled)
            return new RepeatEligibility(false, BusinessViolationCodes.InvalidPrescriptionTransition, "This prescription was cancelled by the vet.");

        if (status == PrescriptionStatus.Issued)
            return new RepeatEligibility(false, BusinessViolationCodes.FirstSupplyNotDispensed, "The first supply has not been dispensed yet.");

        if (suppliesLeft <= 0)
            return new RepeatEligibility(false, BusinessViolationCodes.NoRepeatsRemaining, "There are no repeats left on this prescription.");

        if (today > validUntil)
            return new RepeatEligibility(false, BusinessViolationCodes.PrescriptionValidityEnded, PharmacyMessages.PrescriptionValidityEnded(validUntil));

        if (hasOpenRequest)
            return new RepeatEligibility(false, BusinessViolationCodes.RepeatRequestAlreadyOpen, "A repeat has already been requested.");

        return new RepeatEligibility(true, null, null);
    }

    /// <summary>
    /// The actions the caller may take on the prescription now. <paramref name="effectiveStatus"/> is
    /// <see cref="PrescriptionActivityRules.EffectiveStatus"/>; <paramref name="dispensableToday"/> is "inside <c>ValidUntil</c> with supplies left".
    /// </summary>
    public static IReadOnlyList<string> AllowedActions(
        IEnumerable<string> callerRoles, PrescriptionStatus effectiveStatus, bool dispensableToday, bool alreadyRenewed, bool hasOpenRepeatRequest)
    {
        var roles = callerRoles.ToHashSet(StringComparer.Ordinal);
        var allowed = new List<string>();
        var isVet = roles.Contains(StaffRoles.Vet);
        var canDispense = roles.Overlaps([StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Administrator]);

        if (canDispense && dispensableToday && IsAllowed(effectiveStatus, PrescriptionActions.Dispense))
            allowed.Add(PrescriptionActions.Dispense);

        if (isVet && IsAllowed(effectiveStatus, PrescriptionActions.Cancel))
            allowed.Add(PrescriptionActions.Cancel);

        if (isVet && !alreadyRenewed && IsAllowed(effectiveStatus, PrescriptionActions.Renew))
            allowed.Add(PrescriptionActions.Renew);

        if (isVet && hasOpenRepeatRequest && IsAllowed(effectiveStatus, PrescriptionActions.DeclineRepeat))
            allowed.Add(PrescriptionActions.DeclineRepeat);

        return allowed;
    }
}

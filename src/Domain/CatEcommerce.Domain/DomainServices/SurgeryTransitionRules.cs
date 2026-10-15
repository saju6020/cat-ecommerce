using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SRG-001 §8.3 (Appendix C, ADR-157) -- the one table of allowed <c>(current status, action)</c> pairs, used both by
/// <see cref="Aggregates.Surgery.SurgeryAggregate"/> to refuse an invalid transition and by <c>GetSurgeryDetailQuery</c> to compute
/// <c>allowedTransitions[]</c>, so the two can never disagree. Pure.
/// </summary>
public static class SurgeryTransitionRules
{
    public const string Reschedule = nameof(Reschedule);
    public const string Cancel = nameof(Cancel);
    public const string Admit = nameof(Admit);
    public const string Start = nameof(Start);
    public const string EndProcedure = nameof(EndProcedure);
    public const string SetRecoveryStatus = nameof(SetRecoveryStatus);
    public const string Discharge = nameof(Discharge);
    public const string CloseFollowUp = nameof(CloseFollowUp);

    private static readonly Dictionary<string, SurgeryStatus[]> AllowedFrom = new(StringComparer.Ordinal)
    {
        [Reschedule] = [SurgeryStatus.Scheduled],
        [Cancel] = [SurgeryStatus.Scheduled, SurgeryStatus.PreOp],
        [Admit] = [SurgeryStatus.Scheduled],
        [Start] = [SurgeryStatus.PreOp],
        [EndProcedure] = [SurgeryStatus.InTheatre],
        [SetRecoveryStatus] = [SurgeryStatus.Recovery],
        [Discharge] = [SurgeryStatus.Recovery],
        [CloseFollowUp] = [SurgeryStatus.Discharged],
    };

    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.Ordinal)
    {
        [Reschedule] = "rescheduled",
        [Cancel] = "cancelled",
        [Admit] = "admitted",
        [Start] = "started",
        [EndProcedure] = "ended",
        [SetRecoveryStatus] = "given a recovery status",
        [Discharge] = "discharged",
        [CloseFollowUp] = "given a follow-up closure",
    };

    /// <summary>Throws <c>InvalidSurgeryTransition</c> naming both the current state and the attempted action.</summary>
    public static void EnsureAllowed(SurgeryStatus current, string action)
    {
        if (IsAllowed(current, action)) return;

        throw new DomainRuleViolationException(
            BusinessViolationCodes.InvalidSurgeryTransition,
            $"A surgery {StatePhrase(current)} cannot be {Describe(action)}.",
            action);
    }

    public static bool IsAllowed(SurgeryStatus current, string action) =>
        AllowedFrom.TryGetValue(action, out var allowed) && allowed.Contains(current);

    /// <summary>Every action the aggregate would currently accept -- the read side allowedTransitions[] (§12.4).</summary>
    public static IReadOnlyList<string> AllowedActions(SurgeryStatus current) =>
        AllowedFrom.Where(kv => kv.Value.Contains(current)).Select(kv => kv.Key).ToList();

    private static string Describe(string action) => Descriptions.GetValueOrDefault(action, action);

    private static string StatePhrase(SurgeryStatus status)
    {
        if (status == SurgeryStatus.PreOp) return "in pre-op";
        if (status == SurgeryStatus.InTheatre) return "in theatre";
        if (status == SurgeryStatus.Recovery) return "in recovery";
        return "that is " + status.DisplayName.ToLowerInvariant();
    }
}

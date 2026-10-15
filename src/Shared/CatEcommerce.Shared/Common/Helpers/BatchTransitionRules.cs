using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>HO-IMP-001 §8.6.1 -- the batch state machine as one table. Anything else is
/// <c>InvalidBatchTransition</c>. <c>WithdrawRecall</c> goes to <c>Quarantined</c>, never straight back to sale.</summary>
public static class BatchTransitionRules
{
    public const string Quarantine = "quarantine";
    public const string Release = "release";
    public const string Recall = "recall";
    public const string WithdrawRecall = "withdraw-recall";
    public const string WriteOff = "write-off";

    public static readonly IReadOnlyList<string> Actions = [Quarantine, Release, Recall, WithdrawRecall, WriteOff];

    private static readonly IReadOnlyDictionary<(string Action, BatchStatus From), BatchStatus> Table =
        new Dictionary<(string, BatchStatus), BatchStatus>
        {
            [(Quarantine, BatchStatus.Available)] = BatchStatus.Quarantined,
            [(Release, BatchStatus.Quarantined)] = BatchStatus.Available,
            [(Recall, BatchStatus.Available)] = BatchStatus.Recalled,
            [(Recall, BatchStatus.Quarantined)] = BatchStatus.Recalled,
            [(WithdrawRecall, BatchStatus.Recalled)] = BatchStatus.Quarantined,
            [(WriteOff, BatchStatus.Available)] = BatchStatus.WrittenOff,
            [(WriteOff, BatchStatus.Quarantined)] = BatchStatus.WrittenOff,
            [(WriteOff, BatchStatus.Recalled)] = BatchStatus.WrittenOff,
        };

    public static BatchStatus? Next(BatchStatus from, string action) =>
        Table.TryGetValue((action, from), out var to) ? to : null;

    public static bool IsAllowed(BatchStatus from, string action) => Next(from, action) is not null;

    /// <summary>The actions a staff screen may offer for a batch in <paramref name="status"/> (<c>allowedActions[]</c>, §12.4).</summary>
    public static IReadOnlyList<string> AllowedActions(BatchStatus status) =>
        Actions.Where(a => IsAllowed(status, a)).ToList();
}

namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-ADM-001 §8.5: <c>BranchCalendar.OpenIntervals(...) → IReadOnlyList&lt;TimeInterval&gt;</c> --
/// "instants (<c>DateTimeOffset</c>), sorted, merged". Also <c>IBranchCalendar.OpenIntervalsAsync</c>
/// (§8.9). Field names not given verbatim; <c>Start</c>/<c>End</c> inferred from "instants". Flagged
/// as inferred in docs/handover/CONTRACTS.md.
/// </summary>
public sealed record TimeInterval(DateTimeOffset Start, DateTimeOffset End);

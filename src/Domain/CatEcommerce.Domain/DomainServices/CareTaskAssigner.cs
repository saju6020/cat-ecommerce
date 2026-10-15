using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

public sealed record AssignableUnit(Guid UnitId, string? Block, string Code);

public sealed record CaretakerShifts(Guid StaffAccountId, IReadOnlyList<StaffAvailableInterval> Intervals);

/// <summary>
/// HO-BRD-001 section 8.9 (FR-ADM-3, ADR-224) -- who a task is guided to. Pure, over pre-loaded shifts (F34). A task goes to a caretaker ON SHIFT at its due instant; with
/// several on shift, units are distributed round-robin by block then code, so every task of one unit in one shift goes to the same caretaker; with none, it is
/// unassigned (the dashboard lists it). Assignment is guidance only: any caretaker may complete any task.
/// </summary>
public static class CareTaskAssigner
{
    public static Guid? Assign(DateTimeOffset dueOn, Guid unitId, IReadOnlyList<AssignableUnit> units, IReadOnlyList<CaretakerShifts> caretakers, ISet<Guid>? excluded = null)
    {
        var onShift = caretakers
            .Where(c => excluded is null || !excluded.Contains(c.StaffAccountId))
            .Where(c => c.Intervals.Any(i => i.Start <= dueOn && dueOn < i.End))
            .OrderBy(c => c.StaffAccountId)
            .ToList();

        if (onShift.Count == 0) return null;

        var ordered = units.OrderBy(u => u.Block, StringComparer.Ordinal).ThenBy(u => u.Code, StringComparer.Ordinal).ToList();
        var index = ordered.FindIndex(u => u.UnitId == unitId);
        if (index < 0) index = 0;

        return onShift[index % onShift.Count].StaffAccountId;
    }
}

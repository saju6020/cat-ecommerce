using CatEcommerce.Domain.Entities;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SRG-001 §8.6 (ADR-160, AC-3) -- which checklist items still stand between a surgery and its start. Pure. <b>Start requires every item Done or
/// Waived</b>, with one exception: <c>ConsentSigned</c> is judged by the consent registry (re-read at start, ADR-161), never by the checklist flag, so it is
/// left out of the checklist's own verdict and the start gate judges it separately.
/// </summary>
public static class PreOpChecklistRules
{
    /// <summary>The five items, in checklist order.</summary>
    public static readonly IReadOnlyList<PreOpChecklistItemKind> Order =
    [
        PreOpChecklistItemKind.FastingInstructionsIssued, PreOpChecklistItemKind.PreAnaestheticBloodWork, PreOpChecklistItemKind.WeightRecorded,
        PreOpChecklistItemKind.ConsentSigned, PreOpChecklistItemKind.AsaGradeAssessed,
    ];

    /// <summary>Items neither Done nor Waived, in checklist order. <paramref name="consentJudgedElsewhere"/> leaves <c>ConsentSigned</c> out (the start gate).</summary>
    public static IReadOnlyList<PreOpChecklistItemKind> Outstanding(IEnumerable<PreOpChecklistItem> items, bool consentJudgedElsewhere = false) =>
        items.Where(i => i.Status == ChecklistItemStatus.Outstanding && !(consentJudgedElsewhere && i.Kind == PreOpChecklistItemKind.ConsentSigned))
            .OrderBy(i => Order.ToList().IndexOf(i.Kind))
            .Select(i => i.Kind)
            .ToList();

    public static bool IsComplete(IEnumerable<PreOpChecklistItem> items, bool consentJudgedElsewhere = false) =>
        Outstanding(items, consentJudgedElsewhere).Count == 0;

    /// <summary>The words an owner-facing or staff-facing message uses for an item (U-C6: "naming weight recorded").</summary>
    public static string Describe(PreOpChecklistItemKind kind)
    {
        if (kind == PreOpChecklistItemKind.FastingInstructionsIssued) return "fasting instructions issued";
        if (kind == PreOpChecklistItemKind.PreAnaestheticBloodWork) return "pre-anaesthetic blood work";
        if (kind == PreOpChecklistItemKind.WeightRecorded) return "weight recorded";
        if (kind == PreOpChecklistItemKind.ConsentSigned) return "consent signed";
        return "ASA grade assessed";
    }

    public static string Describe(IEnumerable<PreOpChecklistItemKind> kinds) => string.Join(", ", kinds.Select(Describe));

    /// <summary>Whether a weight recorded on <paramref name="recordedOn"/> is fresh enough for a surgery on <paramref name="surgeryLocalDate"/>: on that date or up to
    /// <paramref name="validityDays"/> before it (a weight AFTER the surgery date does not count).</summary>
    public static bool IsWeightInWindow(DateOnly recordedOn, DateOnly surgeryLocalDate, int validityDays) =>
        recordedOn <= surgeryLocalDate && recordedOn >= surgeryLocalDate.AddDays(-validityDays);
}

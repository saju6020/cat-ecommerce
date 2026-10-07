using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Notification;

/// <summary>HO-NTF-001 §8.12/§12.6 -- one row per due reminder. <c>ReferenceId</c> is the vaccine
/// id for a vaccination row, else <see cref="Guid.Empty"/> for a deworming row (there is no
/// separate "treatment type id" to key on).</summary>
public sealed record DueCareReminderRow(Guid CatId, string CatName, Guid OwnerId, string Kind, Guid ReferenceId, string ReferenceName, DateOnly DueOn, int OwnershipGeneration = 0);

/// <summary>HO-NTF-001 §8.12 -- alive cats, active owners, active vaccines; the window is
/// <c>[dueOn - leadDays, dueOn]</c> (overdue items get no reminder, OQ-5).</summary>
public sealed class GetDueCareRemindersQuery : Query<IReadOnlyList<DueCareReminderRow>>
{
    public required DateOnly Today { get; init; }
    public required int VaccinationLeadDays { get; init; }
    public required int DewormingLeadDays { get; init; }
}

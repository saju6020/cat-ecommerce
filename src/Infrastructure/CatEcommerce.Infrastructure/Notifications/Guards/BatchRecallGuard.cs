using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Notifications.Guards;

/// <summary>
/// HO-IMP-001 section 8.12 -- a recall notice (<paramref name="subjectId"/> is the batch) is valid ONLY while the batch is <see cref="BatchStatus.Recalled"/>. A
/// recall withdrawn as a mistake sends the batch to <c>Quarantined</c>, so notices still waiting (a quiet-hours hold, a retry) fail this guard and are cancelled
/// by HO-NTF-001 as <c>SubjectNoLongerApplies</c>, never sent. Reads only.
/// </summary>
public sealed class BatchRecallGuard(IAggregateRootRepository<BatchAggregate> batches) : INotificationSubjectGuard
{
    public NotificationSubjectKind Kind => NotificationSubjectKind.BatchRecall;

    public async Task<bool> IsStillValidAsync(Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default)
    {
        if (subjectId is null) return false;

        var batch = await batches.GetByIdAsync(subjectId.Value);
        return batch is { Status: var status } && status == BatchStatus.Recalled;
    }
}

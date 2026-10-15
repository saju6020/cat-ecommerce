using CatEcommerce.Domain.Aggregates.Reporting;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-RPT-001 section 8.9 / 11.4 C5 (ADR-287) -- saves a generated run together with the notification requests enlisted for it, in ONE save. If another worker already produced this slot, the unique index
/// <c>ix_scheduled_report_run_slot</c> refuses the save and this returns <c>false</c> -- success with nothing written (the one save carried the duplicate run and its requests together, so none persist).
/// Same shape as <c>INotificationRequestStore</c>: the 23505 is caught in Infrastructure so the Application layer never sees a provider exception.
/// </summary>
public interface IScheduledReportRunStore
{
    Task<bool> TrySaveAsync(ScheduledReportRunAggregate run, CancellationToken ct = default);

    /// <summary>Forgets everything enlisted and not yet saved (the run's requests), after a failure before the save, so a later save in the same scope cannot flush them.</summary>
    void ClearPending();
}

using CatEcommerce.Domain.Aggregates.Reporting;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Reporting;

/// <summary>HO-RPT-001 section 11.4 C5. See <see cref="IScheduledReportRunStore"/>. A refused or failed save leaves nothing tracked: the scoped context is cleared so a later save in the same scope (the Failed-run record) cannot re-send the run or its requests.</summary>
public sealed class EfScheduledReportRunStore(IAggregateRootRepository<ScheduledReportRunAggregate> repository, CatEcommerceDbContext db) : IScheduledReportRunStore
{
    public const string SlotIndexName = "ix_scheduled_report_run_slot";

    public void ClearPending() => db.ChangeTracker.Clear();

    public async Task<bool> TrySaveAsync(ScheduledReportRunAggregate run, CancellationToken ct = default)
    {
        try
        {
            await repository.SaveAsync(run, EventPublishOption.InMemoryPublishOnly);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: SlotIndexName })
        {
            db.ChangeTracker.Clear();
            return false;
        }
        catch
        {
            // Any other failure: nothing of this attempt may stay tracked, or the Failed-run save that follows in the same scope would flush the run and its requests.
            db.ChangeTracker.Clear();
            throw;
        }
    }
}

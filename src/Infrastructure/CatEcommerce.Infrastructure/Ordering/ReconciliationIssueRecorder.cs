using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>
/// HO-ORD-001 §8.16, §12.2 (ADR-205) -- <see cref="IReconciliationIssueRecorder"/>: opens its <b>own DI scope</b> (the one Singleton-to-Scoped exception
/// pattern, <c>.ai/di-registration-guide.md</c> §4 -- here a Scoped service creating a sibling scope), so the issue is saved by a context the failed unit
/// of work cannot have poisoned or rolled back. It never throws out: a failure of its own is logged at <c>Critical</c> with the ids only (never the
/// details staff entered) and <c>null</c> is returned.
/// </summary>
public sealed class ReconciliationIssueRecorder(IServiceScopeFactory scopeFactory, ILogger<ReconciliationIssueRecorder> logger) : IReconciliationIssueRecorder
{
    public async Task<Guid?> RecordAsync(ReconciliationIssueDto dto, CancellationToken ct = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var issues = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<OrderReconciliationIssueAggregate>>();

            var issue = new OrderReconciliationIssueAggregate(dto.IssueId);
            issue.Raise(dto);
            await issues.SaveAsync(issue, EventPublishOption.InMemoryPublishOnly);
            return issue.Id;
        }
        catch (Exception ex)
        {
            logger.LogCritical("A reconciliation issue could not be recorded for order {OrderId}, correlation {CorrelationId} ({ExceptionType}). The staff member's entry is lost.",
                dto.OrderId, dto.CorrelationId, ex.GetType().Name);
            return null;
        }
    }
}

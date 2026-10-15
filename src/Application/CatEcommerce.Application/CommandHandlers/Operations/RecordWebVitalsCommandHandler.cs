using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 11.4 C11. The batch is built under the <b>anonymous</b> user context regardless of who called (a signed-in customer id never
/// reaches the row, ADR-309) and saved without publishing: there is no event.
/// </summary>
public sealed class RecordWebVitalsCommandHandler(IAggregateRootRepository<WebVitalBatchAggregate> batches, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordWebVitalsCommand>
{
    public static readonly UserContext Anonymous = new(SystemActors.Anonymous, SystemActors.Anonymous);

    public async Task<CommandResponse> HandleAsync(RecordWebVitalsCommand command)
    {
        var batch = new WebVitalBatchAggregate(command.BatchId);
        try
        {
            batch.Record(new WebVitalBatchDto(
                timeProvider.GetUtcNow(), command.BuildId,
                command.Samples.Select(s => new WebVitalSampleDto(
                    Enumeration.FromDisplayName<WebVitalMetric>(s.Metric), s.Value, s.RouteTemplate, s.NavigationType, s.EffectiveConnectionType,
                    Enumeration.FromDisplayName<ViewportClass>(s.ViewportClass))).ToList(),
                Anonymous));
        }
        catch (DomainRuleViolationException ex)
        {
            return OperationsResponses.Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(WebVitalBatchAggregate));
        }

        await batches.SaveAsync(batch, EventPublishOption.DoNotPublish);
        return OperationsResponses.Ok();
    }
}

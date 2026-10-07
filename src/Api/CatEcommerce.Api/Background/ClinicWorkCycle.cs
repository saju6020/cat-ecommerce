using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Read.Queries.Clinic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-APT-001 §12.2/§8.11 -- one pass: close past-date waitlist entries, lapse expired offers, then
/// offer freed slots (in that order, so a just-lapsed slot can be re-offered the same pass only on
/// the next poll -- never re-entrant within one pass). Scoped -- <see cref="ClinicWorker"/> creates
/// one scope per poll and resolves this from it; per-item dispatches use fresh scopes from the
/// injected <see cref="IServiceScopeFactory"/>, exactly like <c>NotificationWorkCycle</c>.
/// </summary>
public sealed class ClinicWorkCycle(ILogger<ClinicWorkCycle> logger, IServiceScopeFactory scopeFactory, IInMemoryDispatcher dispatcher, TimeProvider timeProvider)
{
    private static readonly Guid ClinicWorkerActorId = new("00000000-0000-0000-0000-000000000ff1");
    private static UserContext WorkerContext => new(ClinicWorkerActorId, Guid.Empty);

    public async Task RunAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();

        // CODE_REVIEW gate fix (2026-09-28, HIGH): "today" for closing a stale entry is branch-local
        // (§8.11), not one global UTC date -- GetPastWaitlistEntriesQueryHandler now compares each
        // row against its own branch's local date, so `now` is passed through instead of a
        // precomputed DateOnly.
        var pastEntries = await dispatcher.GetResultAsync(new GetPastWaitlistEntriesQuery { Now = now, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() });
        foreach (var id in pastEntries.Result ?? [])
            await DispatchAsync(new CloseStaleWaitlistEntriesCommand { WaitlistEntryId = id, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true }, id, ct);

        var lapsedOffers = await dispatcher.GetResultAsync(new GetLapsedWaitlistOffersQuery { Now = now, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() });
        foreach (var id in lapsedOffers.Result ?? [])
            await DispatchAsync(new LapseWaitlistOfferCommand { WaitlistEntryId = id, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true }, id, ct);

        var candidates = await dispatcher.GetResultAsync(new GetWaitlistOfferCandidatesQuery { Now = now, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() });
        foreach (var id in candidates.Result ?? [])
            await DispatchAsync(new OfferWaitlistSlotCommand { WaitlistEntryId = id, AppointmentId = Guid.NewGuid(), UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true }, id, ct);
    }

    private async Task DispatchAsync<TCommand>(TCommand command, Guid id, CancellationToken ct) where TCommand : Command
    {
        using var scope = scopeFactory.CreateScope();
        try
        {
            var scopedDispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
            await scopedDispatcher.SendAsync(command);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown, not a per-item failure.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Clinic worker pass failed for {CommandType} {Id}.", typeof(TCommand).Name, id);
        }
    }
}

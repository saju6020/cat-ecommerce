using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Read.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-BRD-001 section 12.2 (ADR-231, renumbered ADR-364) -- one pass of the Boarding worker, in order: confirm reservations whose deposit Billing shows ready, lapse
/// expired reservations, expire past enquiries; also generates care tasks, escalates incidents and invoices cycles (Phase G). Later phases append the daily updates and the waitlist. EVERY
/// item is one command through <see cref="IInMemoryDispatcher"/> in its own scope, exactly like a controller sends it; a failing item is logged and the pass goes on.
/// Scoped -- <see cref="BoardingWorker"/> creates one scope per poll; tests call <see cref="RunAsync"/> directly.
/// </summary>
public sealed class BoardingWorkCycle(ILogger<BoardingWorkCycle> logger, IServiceScopeFactory scopeFactory, IInMemoryDispatcher dispatcher, TimeProvider timeProvider)
{
    private static readonly Guid WorkerActorId = new("00000000-0000-0000-0000-000000000ff2");
    private static UserContext WorkerContext => new(WorkerActorId, Guid.Empty);

    public async Task RunAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();

        await ForEachAsync(new GetReservationsReadyToConfirmQuery { Now = now, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() },
            id => new ConfirmBoardingStayCommand { StayId = id, IsWorker = true, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true }, ct);

        // Care tasks: today and tomorrow of every checked-in stay (cheap when done -- the handler skips every key that exists).
        IReadOnlyList<InHouseStayViewModel> inHouse = [];
        try
        {
            inHouse = (await dispatcher.GetResultAsync(new GetStaysInHouseQuery { Now = now, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() })).Result ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Boarding worker could not list the stays in house.");
        }

        foreach (var stay in inHouse)
            foreach (var date in new[] { stay.LocalToday, stay.LocalToday.AddDays(1) })
                await DispatchAsync(new GenerateCareTasksCommand { StayId = stay.StayId, LocalDate = date, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true }, stay.StayId, ct);

        // High incidents the owner has not acknowledged: the handler applies the branch's own escalation minutes (an early one is a no-op).
        await ForEachAsync(new GetIncidentsToEscalateQuery { Now = now, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() },
            id => new EscalateIncidentCommand { IncidentId = id, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true }, ct);

        // US-BRD-20: a cycle invoice when a stay reaches a 30-night boundary with uninvoiced charges; the FINAL invoice (cycle 0) for a checked-out stay with charges left.
        IReadOnlyList<StayInvoiceDueViewModel> due = [];
        try
        {
            due = (await dispatcher.GetResultAsync(new GetStaysDueForInvoicingQuery { Now = now, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() })).Result ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Boarding worker could not list the stays due for invoicing.");
        }

        foreach (var stay in due)
        {
            if (ct.IsCancellationRequested) return;
            await DispatchAsync(new InvoiceBoardingCycleCommand
            { StayId = stay.StayId, Cycle = stay.Cycle, ServiceDateTo = stay.ServiceDateTo, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true }, stay.StayId, ct);
        }

        // Daily updates at the branch's hour (deduplicated per stay and day; guarded while the stay is in house).
        IReadOnlyList<DailyUpdateCandidateViewModel> updates = [];
        try
        {
            updates = (await dispatcher.GetResultAsync(new GetDailyUpdateCandidatesQuery { Now = now, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() })).Result ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Boarding worker could not list the daily updates.");
        }

        foreach (var update in updates)
        {
            if (ct.IsCancellationRequested) return;
            await DispatchAsync(new SendBoardingDailyUpdateCommand { StayId = update.StayId, LocalDate = update.LocalDate, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true }, update.StayId, ct);
        }

        // Waitlist: notices for the oldest entries with a free unit, then close the stale ones.
        await ForEachAsync(new GetWaitlistCandidatesQuery { Now = now, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() },
            id => new NotifyWaitlistEntryCommand { EntryId = id, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true }, ct);
        await ForEachAsync(new GetStaleWaitlistEntriesQuery { Now = now, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() },
            id => new CloseStaleBoardingWaitlistCommand { EntryId = id, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true }, ct);

        await ForEachAsync(new GetExpiredReservationsQuery { Now = now, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() },
            id => new LapseBoardingReservationCommand { StayId = id, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true }, ct);

        await ForEachAsync(new GetPastEnquiriesQuery { Now = now, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() },
            id => new ExpireBoardingEnquiryCommand { StayId = id, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true }, ct);
    }

    private async Task ForEachAsync(Query<IReadOnlyList<Guid>> query, Func<Guid, Command> command, CancellationToken ct)
    {
        IReadOnlyList<Guid> ids;
        try
        {
            ids = (await dispatcher.GetResultAsync(query)).Result ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Boarding worker could not list {Query}.", query.GetType().Name);
            return;
        }

        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) return;
            await DispatchAsync(command(id), id, ct);
        }
    }

    private async Task DispatchAsync(Command command, Guid id, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        try
        {
            var response = await scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>().SendAsync(command);
            if (!response.IsSuccess)
                logger.LogWarning("Boarding worker command {CommandType} for {Id} was refused: {Codes}.", command.GetType().Name, id,
                    string.Join(",", response.ValidationResult?.Errors.Select(e => e.ErrorCode) ?? []));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown, not a per-item failure.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Boarding worker pass failed for {CommandType} {Id}.", command.GetType().Name, id);
        }
    }
}

using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Api.Background;

/// <summary>Remembers when the refill sweep last ran, so the hourly sweep is not repeated on every 60-second poll. A singleton; tests call the sweep directly.</summary>
public sealed class PharmacyWorkerState
{
    private readonly object _gate = new();
    private DateTimeOffset? _lastRefillSweep;

    public bool TryStartRefillSweep(DateTimeOffset now, int intervalMinutes)
    {
        lock (_gate)
        {
            if (_lastRefillSweep is { } last && now - last < TimeSpan.FromMinutes(intervalMinutes)) return false;
            _lastRefillSweep = now;
            return true;
        }
    }
}

/// <summary>
/// HO-PHM-001 §12.2 -- one pass of the pharmacy worker. Each pass is isolated (one that throws is logged and the next still runs, U-X4) and every unit of work is ONE command
/// dispatched in its OWN scope (its own <c>DbContext</c>), so one bad item is logged and never stops the pass. Idempotent -- a pass that finds nothing new dispatches nothing.
/// Scoped; callable by tests (workers are off in <c>Testing</c>). The passes, in order: (1) seal the register, (2) expire diet authorisations, (3) expire prescriptions, (4) at most
/// hourly, refill reminders. <b>Nothing safety-relevant waits for any of them</b> (ADR-142, ADR-149).
/// </summary>
public sealed class PharmacyWorkCycle(
    ILogger<PharmacyWorkCycle> logger,
    IServiceScopeFactory scopeFactory,
    IInMemoryDispatcher dispatcher,
    IBusinessSettings settings,
    IPharmacyClock clock,
    INotificationLinkBuilder links,
    PharmacyWorkerState state,
    TimeProvider timeProvider,
    Microsoft.Extensions.Options.IOptions<CatEcommerce.Infrastructure.Configuration.PharmacyOptions> options)
{
    private static UserContext WorkerContext => new(PharmacySystemActors.PharmacyWorker, Guid.Empty);

    public async Task RunAsync(CancellationToken ct = default)
    {
        await RunPassAsync("seal", token => RunSealPassAsync(token), ct);
        await RunPassAsync("diet-expiry", token => RunDietExpiryPassAsync(token), ct);
        await RunPassAsync("prescription-expiry", token => RunPrescriptionExpiryPassAsync(token), ct);
        if (state.TryStartRefillSweep(timeProvider.GetUtcNow(), options.Value.Worker.RefillSweepIntervalMinutes)) await RunPassAsync("refill-reminders", token => RunRefillSweepAsync(token), ct);
    }

    private async Task RunPassAsync(string name, Func<CancellationToken, Task> pass, CancellationToken ct)
    {
        try { await pass(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogError(ex, "Pharmacy worker {Pass} pass failed; the next pass still runs.", name); }
    }

    /// <summary>§8.9 / ADR-145 -- seal every branch that has register entries, one command per branch (at most one chunk of entries each).</summary>
    public async Task RunSealPassAsync(CancellationToken ct = default, Guid? onlyBranchId = null)
    {
        var branches = (await dispatcher.GetResultAsync(new GetBranchesWithUnsealedRegisterEntriesQuery { UserContext = WorkerContext, CorrelationId = Guid.NewGuid() })).Result ?? [];

        foreach (var branchId in branches.Where(b => onlyBranchId is null || b == onlyBranchId))
        {
            // Keep sealing the branch while a chunk was full: a long backlog clears in one pass, not one chunk per poll.
            while (!ct.IsCancellationRequested)
            {
                var result = await DispatchAsync(
                    new SealControlledDrugEntriesCommand { BranchId = branchId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true },
                    $"sealing of branch {branchId}", ct);

                if (result is not SealingResult { Sealed: >= ControlledDrugRules.SealBatchSize }) break;
            }
        }
    }

    /// <summary>§8.10 -- record the expiry of authorisations whose last day has passed, raise the event, tell the owner. The gate already locked the product the moment the date passed.</summary>
    public async Task RunDietExpiryPassAsync(CancellationToken ct = default)
    {
        var due = (await dispatcher.GetResultAsync(new GetExpirableDietAuthorisationsQuery { UserContext = WorkerContext, CorrelationId = Guid.NewGuid() })).Result ?? [];
        foreach (var authorisationId in due)
        {
            await DispatchAsync(
                new ExpireDietAuthorisationCommand { AuthorisationId = authorisationId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true },
                $"expiry of diet authorisation {authorisationId}", ct);
        }
    }

    /// <summary>ADR-142 -- record the expiry of prescriptions whose validity has passed with supplies left. Dispensing never waited for this.</summary>
    public async Task RunPrescriptionExpiryPassAsync(CancellationToken ct = default)
    {
        var due = (await dispatcher.GetResultAsync(new GetExpirablePrescriptionsQuery { UserContext = WorkerContext, CorrelationId = Guid.NewGuid() })).Result ?? [];
        foreach (var prescriptionId in due)
        {
            await DispatchAsync(
                new ExpirePrescriptionCommand { PrescriptionId = prescriptionId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true },
                $"expiry of prescription {prescriptionId}", ct);
        }
    }

    /// <summary>
    /// §8.11 / US-NTF-03 AC1 -- one <c>pharmacy.refill-due</c> request per prescription in its reminder window, deduplicated by <c>pharmacy:refill:{rx}:{supply}</c> so running the sweep twice
    /// (or every hour) enqueues it once, and guarded at send time by <c>PrescriptionRefillDueGuard</c> so a refill dispensed meanwhile cancels it. The main branch's local date is "today".
    /// </summary>
    public async Task RunRefillSweepAsync(CancellationToken ct = default)
    {
        var lead = settings.GetInt(SettingKeys.PharmacyRefillReminderLeadDays);
        if (lead <= 0) return;

        var today = await clock.MainBranchTodayAsync(ct);
        var due = (await dispatcher.GetResultAsync(new GetDueRefillRemindersQuery { Today = today, LeadDays = lead, UserContext = WorkerContext, CorrelationId = Guid.NewGuid() })).Result ?? [];

        foreach (var row in due)
        {
            var variables = new Dictionary<string, string>
            {
                ["catName"] = row.CatName,
                ["medicineName"] = row.MedicineName,
                ["refillDueOn"] = row.RefillDueOn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                ["validUntil"] = row.ValidUntil.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                ["repeatLink"] = links.RepeatRequestLink(row.PrescriptionId),
            };

            await DispatchAsync(new RequestNotificationCommand
            {
                RequestId = Guid.NewGuid(), TemplateKey = NotificationTemplateCatalogue.PharmacyRefillDue, RecipientKind = NotificationRecipientKind.Customer.DisplayName,
                RecipientId = row.CustomerId, Variables = variables, SubjectKind = NotificationSubjectKind.PrescriptionRefillDue.DisplayName, SubjectId = row.PrescriptionId,
                SubjectDueOn = row.RefillDueOn, BranchId = row.BranchId, DeduplicationKey = $"pharmacy:refill:{row.PrescriptionId:N}:{row.SupplyNumber}",
                UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, $"refill reminder for prescription {row.PrescriptionId}", ct);
        }
    }

    private async Task<object?> DispatchAsync(Command command, string what, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return null;

        using var scope = scopeFactory.CreateScope();
        try
        {
            var response = await scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>().SendAsync(command);
            if (!response.IsSuccess)
            {
                logger.LogWarning("Pharmacy worker: {What} was refused.", what);
                return null;
            }

            return response.Result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pharmacy worker failed for {What}.", what);
            return null;
        }
    }
}

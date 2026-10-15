using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Boarding.BoardingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Boarding;

/// <summary>
/// C12 -- US-BRD-08. Staff (or a caretaker, ADR-366) check the cats in against the checklist. The handler resolves the files named for belongings photos (HO-FIL-001: purpose
/// BoardingBelongings, uploaded by the caller, inside the attach window), re-evaluates the gate, and leaves everything else to <see cref="BoardingStayAggregate.CheckIn"/>. The
/// charges join Billing in the same save (<see cref="BoardingCharges"/>); the message <c>boarding.checked-in</c> is enqueued before the save; the cat's weight reaches HO-CAT-001's weight history through an event handler. Care tasks are NOT created
/// here (rule 1): the worker's next pass generates today's list.
/// </summary>
public sealed class CheckInBoardingStayCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<StoredFileAggregate> fileRepository,
    IBoardingInputs boardingInputs,
    IBoardingReferenceReader referenceReader,
    IBoardingChargeLedger chargeLedger,
    IBillableChargeRecorder chargeRecorder,
    INotificationOutbox notificationOutbox,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CheckInBoardingStayCommand>
{
    public async Task<CommandResponse> HandleAsync(CheckInBoardingStayCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        var settings = await boardingInputs.GetSettingsAsync(stay.BranchId);
        if (settings is null) return Violation(BusinessViolationCodes.BranchNotFound, "BranchId", "Branch not found.");

        var now = timeProvider.GetUtcNow();
        var cats = new List<CheckInCatDto>();
        foreach (var input in command.Cats)
        {
            var belongings = new List<BelongingInput>();
            foreach (var item in input.Belongings)
            {
                foreach (var fileId in item.FileIds)
                {
                    var file = await fileRepository.GetByIdAsync(fileId);
                    if (file is null || !file.IsEligibleForAttach(FilePurpose.BoardingBelongings, command.UserContext!.UserId, now, FileRules.AttachWindow))
                        return Violation(BusinessViolationCodes.InvalidMediaReference, "FileIds", "One of the photos could not be attached. Upload it again and try immediately.");
                }

                belongings.Add(new BelongingInput(BoardingUnitMapper.ParseEnum<BelongingKind>(item.Kind), item.Description, item.FileIds));
            }

            SignatureCapture? signature = input.Signature is null ? null
                : new SignatureCapture(input.Signature.SignerName, BoardingUnitMapper.ParseEnum<SignatureMethod>(input.Signature.Method), input.Signature.Strokes, input.Signature.ConfirmedStatement, now);

            cats.Add(new CheckInCatDto(input.CatId, input.IdentityConfirmed, input.VaccinationProofVerified, input.WeightKg, input.ArrivalHealthObservation, belongings, input.NoBelongings, signature));
        }

        var catIds = stay.Cats.Select(c => c.CatId).ToList();
        var (loaded, failure) = await StayCatGate.LoadCatsAsync(catRepository, catIds, stay.CustomerId);
        if (failure is not null) return failure;

        var gates = StayCatGate.Evaluate(loaded!, await referenceReader.GetActiveCoreVaccinesAsync(), stay.DepartureOn, settings.VaccinationDueSoonDays);
        var today = BoardingOccupancyWindow.LocalDateOf(now, settings.TimeZoneId);
        try
        {
            stay.CheckIn(new CheckInDto(cats, gates, today, now, settings.RequireBelongingsPhotos, command.UserContext!, command.CorrelationId));

            // The stay's charges are pushed to Billing here (not at reserve: a lapsed reservation leaves no phantom charges), in the same save as the check-in.
            var plan = await BoardingCharges.SyncAsync(stay, chargeLedger, chargeRecorder);
            stay.ApplyChargePlan(plan.Revision, plan.CreditDue, command.CorrelationId, command.UserContext!);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        BoardingNotifications.EnqueueCheckedIn(notificationOutbox, linkBuilder, stay, command.CorrelationId, command.UserContext!);
        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success(stay.Id);
    }
}

/// <summary>
/// C14 -- ADR-224 (renumbered ADR-357). Loads the stay READ-ONLY, runs the pure <see cref="CareTaskGenerator"/> for one local date, assigns each task with the pure
/// <see cref="CareTaskAssigner"/>, and creates only the tasks whose key does not exist yet (<c>SaveAsync</c> per new task; a batch of new roots of ONE aggregate type, the stay
/// never updated). Running it twice inserts nothing the second time; a racing duplicate hits <c>ix_care_task_generation</c> and is a success.
/// </summary>
public sealed class GenerateCareTasksCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<CareTaskAggregate> taskRepository,
    IAggregateRootRepository<BoardingCareRoutineAggregate> routineRepository,
    IBoardingInputs boardingInputs,
    IBoardingCareInputs careInputs,
    IUniqueViolationDetector uniqueViolation,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<GenerateCareTasksCommand>
{
    public async Task<CommandResponse> HandleAsync(GenerateCareTasksCommand command)
    {
        var stay = await stayRepository.GetByIdAsync(command.StayId);
        if (stay is not { Status: var status } || status != BoardingStayStatus.CheckedIn || stay.CheckedInOn is null) return Success(0);

        var settings = await boardingInputs.GetSettingsAsync(stay.BranchId);
        var inputs = await boardingInputs.LoadAsync(stay.BranchId, stay.ArrivalOn, stay.DepartureOn, stay.Id, timeProvider.GetUtcNow());
        if (settings is null || inputs is null) return Success(0);

        var noon = BoardingOccupancyWindow.InstantOf(command.LocalDate, new TimeOnly(12, 0), settings.TimeZoneId);
        var allocation = stay.Allocations.Where(a => a.IsActive && a.OccupiedFrom <= noon && noon < a.OccupiedTo).OrderByDescending(a => a.OccupiedFrom).FirstOrDefault() ?? stay.CurrentAllocation;
        if (allocation is null) return Success(0);

        var catInputs = stay.Cats.Where(c => c.Profile is not null).Select(c => new CareTaskCatInput(c.CatId, c.CatName, allocation.UnitId, c.Profile!)).ToList();
        var routine = BoardingCareRoutineAggregate.Effective(await routineRepository.GetByIdAsync(stay.BranchId));
        var seeds = CareTaskGenerator.Generate(command.LocalDate, settings.TimeZoneId, stay.ArrivalOn, stay.DepartureOn, stay.CheckedInOn.Value, settings.CheckOutWindow, catInputs, routine);
        if (seeds.Count == 0) return Success(0);

        var shifts = await careInputs.GetCaretakerShiftsAsync(stay.BranchId, command.LocalDate, command.LocalDate);
        var units = inputs.Units.Select(u => new AssignableUnit(u.UnitId, u.Block, u.Code)).ToList();

        var created = 0;
        foreach (var seed in seeds)
        {
            if (await taskRepository.ExistAsync(t => t.StayId == stay.Id && t.CatId == seed.CatId && t.LocalDate == command.LocalDate && t.SourceKey == seed.SourceKey)) continue;

            var task = new CareTaskAggregate(Guid.NewGuid());
            task.Create(stay.BranchId, stay.Id, command.LocalDate, seed, CareTaskAssigner.Assign(seed.DueOn, seed.UnitId, units, shifts), command.UserContext!);
            try
            {
                await taskRepository.SaveAsync(task, EventPublishOption.InMemoryPublishOnly);
            }
            catch (Exception ex) when (uniqueViolation.IsUniqueViolation(ex, "ix_care_task_generation"))
            {
                // A racing writer (a second host, or the extension / care-profile handler) created this key between the check and the save. The failed row stays tracked in this
                // scope, so stop here: the duplicate is a success, and the next pass creates whatever is still missing.
                return Success(created);
            }

            created++;
        }

        return Success(created);
    }
}

/// <summary>C15 -- US-BRD-10. <c>CompletedBy</c> is the caller; completing a Done task succeeds and writes nothing. Any caretaker may complete any task of the branch.</summary>
public sealed class CompleteCareTaskCommandHandler(IAggregateRootRepository<CareTaskAggregate> taskRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<CompleteCareTaskCommand>
{
    public async Task<CommandResponse> HandleAsync(CompleteCareTaskCommand command)
    {
        var task = await taskRepository.GetByIdAsync(command.TaskId);
        if (task is null || task.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.CareTaskNotFound, nameof(command.TaskId), "Task not found.", nameof(CareTaskAggregate));

        try
        {
            task.Complete(command.UserContext!.UserId, timeProvider.GetUtcNow(), command.Note, command.UserContext, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(CareTaskAggregate));
        }

        await taskRepository.UpdateAsync(task, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class SkipCareTaskCommandHandler(IAggregateRootRepository<CareTaskAggregate> taskRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<SkipCareTaskCommand>
{
    public async Task<CommandResponse> HandleAsync(SkipCareTaskCommand command)
    {
        var task = await taskRepository.GetByIdAsync(command.TaskId);
        if (task is null || task.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.CareTaskNotFound, nameof(command.TaskId), "Task not found.", nameof(CareTaskAggregate));

        try
        {
            task.Skip(command.UserContext!.UserId, timeProvider.GetUtcNow(), command.Reason);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(CareTaskAggregate));
        }

        await taskRepository.UpdateAsync(task, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C16 -- US-BRD-11. A cat that is not in a checked-in stay of the branch cannot be observed (<c>ObservationNotAllowed</c>).</summary>
public sealed class RecordCareObservationCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<CareObservationAggregate> observationRepository,
    IBoardingInputs boardingInputs,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordCareObservationCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordCareObservationCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        if (stay.Status != BoardingStayStatus.CheckedIn || stay.Cats.All(c => c.CatId != command.CatId))
            return Violation(BusinessViolationCodes.ObservationNotAllowed, nameof(command.CatId), "Observations can only be recorded for a cat that is currently boarding.");

        var now = timeProvider.GetUtcNow();
        var zone = (await boardingInputs.GetSettingsAsync(stay.BranchId))?.TimeZoneId ?? "UTC";
        var observation = new CareObservationAggregate(command.ObservationId);
        try
        {
            observation.Record(
                stay.BranchId, stay.Id, command.CatId, now, BoardingOccupancyWindow.LocalDateOf(now, zone),
                BoardingUnitMapper.ParseEnum<PortionEaten>(command.PortionEaten), BoardingUnitMapper.ParseEnum<WaterIntake>(command.WaterIntake),
                BoardingUnitMapper.ParseEnum<LitterOutput>(command.LitterOutput), BoardingUnitMapper.ParseEnum<CatMood>(command.Mood), command.Notes,
                command.UserContext!, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(CareObservationAggregate));
        }

        await observationRepository.SaveAsync(observation, EventPublishOption.InMemoryPublishOnly);
        return Success(observation.Id);
    }
}

/// <summary>C17 -- US-BRD-18, BR-9, ADR-227 (renumbered ADR-360). Flags the cat and, when the stay is not in an isolation unit, claims one through <see cref="UnitClaim"/> so the SAME
/// mutation relocates it; none free -> <c>NoIsolationUnitAvailable</c> and nothing changes.</summary>
public sealed class FlagSuspectedInfectiousCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IBoardingInputs boardingInputs,
    TimeProvider timeProvider,
    IFreshScopeExecutor scopes,
    IUniqueViolationDetector uniqueViolation)
    : ICommandHandlerAsync<FlagSuspectedInfectiousCommand>
{
    // Backlog row 59: two flags at once would both pick the first free isolation unit; the loser re-picks in a fresh scope (bounded).
    public Task<CommandResponse> HandleAsync(FlagSuspectedInfectiousCommand command) =>
        ClaimRetry.RunAsync(this, command, (h, c) => h.AttemptAsync(c), scopes, uniqueViolation.IsOccupancyClaimLost,
            () => Violation(BusinessViolationCodes.UnitNoLongerAvailable, "UnitId", BoardingMessages.UnitNoLongerAvailable));

    public async Task<CommandResponse> AttemptAsync(FlagSuspectedInfectiousCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        var now = timeProvider.GetUtcNow();
        AllocatedUnit? target = null;

        if (stay.CurrentAllocation?.UnitType != BoardingUnitType.Isolation)
        {
            var zone = (await boardingInputs.GetSettingsAsync(stay.BranchId))?.TimeZoneId ?? "UTC";
            var from = BoardingOccupancyWindow.LocalDateOf(now, zone);
            if (from < stay.ArrivalOn) from = stay.ArrivalOn;

            var claim = await UnitClaim.ValidateAsync(boardingInputs, new UnitClaim.Request(
                stay.BranchId, from, stay.DepartureOn, BoardingUnitType.Isolation, stay.Cats.Select(c => c.CatId).ToList(), RequiresIsolation: true,
                AvailabilityMode.Staff, stay.Id, now, command.TargetIsolationUnitId));

            // No free isolation unit: the aggregate refuses with NoIsolationUnitAvailable and nothing changes.
            if (claim.IsSuccess) target = claim.Unit;
        }

        try
        {
            stay.FlagSuspectedInfectious(new FlagInfectiousDto(command.CatId, command.Note, target, now, command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class ClearSuspectedInfectiousCommandHandler(IAggregateRootRepository<BoardingStayAggregate> stayRepository)
    : ICommandHandlerAsync<ClearSuspectedInfectiousCommand>
{
    public async Task<CommandResponse> HandleAsync(ClearSuspectedInfectiousCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        try
        {
            stay.ClearSuspectedInfectious(new ClearInfectiousDto(command.CatId, command.Note ?? "", command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await stayRepository.UpdateAsync(stay, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>US-BRD-18 AC2. Only for a checked-in stay that sits in an isolation unit (<c>NotInIsolation</c> otherwise); the staff member is the caller.</summary>
public sealed class RecordIsolationEntryCommandHandler(
    IAggregateRootRepository<BoardingStayAggregate> stayRepository,
    IAggregateRootRepository<IsolationEntryAggregate> entryRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordIsolationEntryCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordIsolationEntryCommand command)
    {
        var stay = await StayAccess.LoadAsync(stayRepository, command.StayId, null, command.BranchId);
        if (stay is null) return StayAccess.NotFound();

        var allocation = stay.CurrentAllocation;
        if (stay.Status != BoardingStayStatus.CheckedIn || allocation?.UnitType != BoardingUnitType.Isolation)
            return Violation(BusinessViolationCodes.NotInIsolation, nameof(command.StayId), "This stay is not in an isolation unit.");

        var entry = new IsolationEntryAggregate(command.EntryId);
        try
        {
            entry.Enter(stay.BranchId, stay.Id, allocation.UnitId, timeProvider.GetUtcNow(), command.Purpose, command.UserContext!);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(IsolationEntryAggregate));
        }

        await entryRepository.SaveAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success(entry.Id);
    }
}

public sealed class RecordIsolationExitCommandHandler(IAggregateRootRepository<IsolationEntryAggregate> entryRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordIsolationExitCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordIsolationExitCommand command)
    {
        var entry = await entryRepository.GetByIdAsync(command.EntryId);
        if (entry is null || entry.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.NotInIsolation, nameof(command.EntryId), "Isolation entry not found.", nameof(IsolationEntryAggregate));

        entry.Exit(timeProvider.GetUtcNow());
        await entryRepository.UpdateAsync(entry, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

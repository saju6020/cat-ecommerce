using CatEcommerce.Application.Commands.Reporting;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Reporting;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Reporting.ReportScheduleSupport;

namespace CatEcommerce.Application.CommandHandlers.Reporting;

/// <summary>HO-RPT-001 section 11.4 C2 -- create a schedule. The report, parameters, period, branch and every recipient are checked here; the aggregate checks its own shape.</summary>
public sealed class CreateReportScheduleCommandHandler(
    IAggregateRootRepository<ReportScheduleAggregate> scheduleRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<StaffBranchAssignmentAggregate> assignmentRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IReportCatalogue catalogue,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CreateReportScheduleCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateReportScheduleCommand command)
    {
        var now = timeProvider.GetUtcNow();
        var (definition, zone, failure) = await ResolveAsync(command, catalogue, branchRepository, staffRepository, now);
        if (failure is not null) return failure;

        var recipientFailure = await CheckRecipientsAsync(definition!, command.BranchId, command.RecipientIds, staffRepository, assignmentRepository);
        if (recipientFailure is not null) return recipientFailure;

        var schedule = new ReportScheduleAggregate(command.ScheduleId);
        try
        {
            schedule.Create(command.ToDomainDto(definition!, zone!, now));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await scheduleRepository.SaveAsync(schedule, EventPublishOption.InMemoryPublishOnly);
        return Success(new ReportScheduleResult(schedule.Id, schedule.NextRunOn));
    }
}

/// <summary>C3 -- change a schedule. A stale <c>ExpectedVersion</c> is a 409; every other rule is C2's. The next run is recomputed from now.</summary>
public sealed class UpdateReportScheduleCommandHandler(
    IAggregateRootRepository<ReportScheduleAggregate> scheduleRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<StaffBranchAssignmentAggregate> assignmentRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IReportCatalogue catalogue,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<UpdateReportScheduleCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateReportScheduleCommand command)
    {
        var schedule = await scheduleRepository.GetByIdAsync(command.ScheduleId);
        if (schedule is null)
            return Violation(BusinessViolationCodes.ReportScheduleNotFound, nameof(command.ScheduleId), ReportingMessages.ReportScheduleNotFound);

        if (schedule.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleVersion, nameof(command.ExpectedVersion), "This schedule has changed since you loaded it. Reload and try again.");

        var now = timeProvider.GetUtcNow();
        var (definition, zone, failure) = await ResolveAsync(command, catalogue, branchRepository, staffRepository, now);
        if (failure is not null) return failure;

        var recipientFailure = await CheckRecipientsAsync(definition!, command.BranchId, command.RecipientIds, staffRepository, assignmentRepository);
        if (recipientFailure is not null) return recipientFailure;

        try
        {
            schedule.Update(command.ToDomainDto(definition!, zone!, now));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await scheduleRepository.UpdateAsync(schedule, EventPublishOption.InMemoryPublishOnly);
        return Success(new ReportScheduleResult(schedule.Id, schedule.NextRunOn));
    }
}

/// <summary>C4 -- pause.</summary>
public sealed class PauseReportScheduleCommandHandler(IAggregateRootRepository<ReportScheduleAggregate> scheduleRepository) : ICommandHandlerAsync<PauseReportScheduleCommand>
{
    public async Task<CommandResponse> HandleAsync(PauseReportScheduleCommand command)
    {
        var schedule = await scheduleRepository.GetByIdAsync(command.ScheduleId);
        if (schedule is null)
            return Violation(BusinessViolationCodes.ReportScheduleNotFound, nameof(command.ScheduleId), ReportingMessages.ReportScheduleNotFound);

        try
        {
            schedule.Pause(command.UserContext!);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await scheduleRepository.UpdateAsync(schedule, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C4 -- resume. The next run is computed from now: the time spent paused is not caught up.</summary>
public sealed class ResumeReportScheduleCommandHandler(
    IAggregateRootRepository<ReportScheduleAggregate> scheduleRepository, IAggregateRootRepository<BranchAggregate> branchRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<ResumeReportScheduleCommand>
{
    public async Task<CommandResponse> HandleAsync(ResumeReportScheduleCommand command)
    {
        var schedule = await scheduleRepository.GetByIdAsync(command.ScheduleId);
        if (schedule is null)
            return Violation(BusinessViolationCodes.ReportScheduleNotFound, nameof(command.ScheduleId), ReportingMessages.ReportScheduleNotFound);

        var zone = await ResolveZoneAsync(branchRepository, schedule.BranchId);
        if (zone is null)
            return Violation(BusinessViolationCodes.BranchNotFound, "branchId", "This schedule's branch no longer exists.");

        try
        {
            schedule.Resume(timeProvider.GetUtcNow(), zone, command.UserContext!);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex);
        }

        await scheduleRepository.UpdateAsync(schedule, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C4 -- cancel (terminal).</summary>
public sealed class CancelReportScheduleCommandHandler(IAggregateRootRepository<ReportScheduleAggregate> scheduleRepository) : ICommandHandlerAsync<CancelReportScheduleCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelReportScheduleCommand command)
    {
        var schedule = await scheduleRepository.GetByIdAsync(command.ScheduleId);
        if (schedule is null)
            return Violation(BusinessViolationCodes.ReportScheduleNotFound, nameof(command.ScheduleId), ReportingMessages.ReportScheduleNotFound);

        schedule.Cancel(command.UserContext!);
        await scheduleRepository.UpdateAsync(schedule, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C7 -- the slot is done. Idempotent: a second call for the same due time changes nothing; any number of missed slots collapse into the one that just ran.</summary>
public sealed class AdvanceReportScheduleCommandHandler(
    IAggregateRootRepository<ReportScheduleAggregate> scheduleRepository, IAggregateRootRepository<BranchAggregate> branchRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<AdvanceReportScheduleCommand>
{
    public async Task<CommandResponse> HandleAsync(AdvanceReportScheduleCommand command)
    {
        var schedule = await scheduleRepository.GetByIdAsync(command.ScheduleId);
        if (schedule is null)
            return Violation(BusinessViolationCodes.ReportScheduleNotFound, nameof(command.ScheduleId), ReportingMessages.ReportScheduleNotFound);

        var zone = await ResolveZoneAsync(branchRepository, schedule.BranchId);
        if (zone is null)
            return Violation(BusinessViolationCodes.BranchNotFound, "branchId", "This schedule's branch no longer exists.");

        var before = schedule.Version;
        schedule.RecordRunCompleted(command.DueOn, timeProvider.GetUtcNow(), zone, command.UserContext!);
        if (schedule.Version != before) await scheduleRepository.UpdateAsync(schedule, EventPublishOption.InMemoryPublishOnly);

        return Success(new ReportScheduleResult(schedule.Id, schedule.NextRunOn));
    }
}

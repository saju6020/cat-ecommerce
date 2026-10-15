using System.Globalization;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Visit;

using static VisitCommandHandlerSupport;

/// <summary>
/// C7 -- the auto-saved working copy (§8.4, ADR-122). The visit must be open; the draft is created on the
/// author's first write and overwritten afterwards. <b>No <c>ExpectedVersion</c>:</b> last write wins within
/// one author's own copy. The draft is excluded from audit change capture (<c>AuditCaptureExclusions</c>)
/// because it is overwritten every few seconds and is not the record. A race of two first writes by the same
/// author is settled by the unique <c>(visit_id, author_staff_id)</c> index; the controller retries once as
/// an overwrite.
/// </summary>
public sealed class SaveConsultationDraftCommandHandler(
    IAggregateRootRepository<VisitAggregate> visitRepository,
    IAggregateRootRepository<ConsultationDraftAggregate> draftRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SaveConsultationDraftCommand>
{
    public async Task<CommandResponse> HandleAsync(SaveConsultationDraftCommand command)
    {
        var visit = await visitRepository.GetByIdAsync(command.VisitId);
        if (visit is null || visit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.VisitNotFound, nameof(command.VisitId), ClinicalMessages.VisitNotFound);

        var authorId = command.UserContext.UserId;
        var existing = await draftRepository.GetByFilterAsync(d => d.VisitId == command.VisitId && d.AuthorStaffId == authorId);
        var draft = existing ?? new ConsultationDraftAggregate(command.DraftId);

        try
        {
            draft.Overwrite(command.ToDomainDto(visit.Status, timeProvider.GetUtcNow()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(ConsultationDraftAggregate));
        }

        if (existing is null)
            await draftRepository.SaveAsync(draft, EventPublishOption.InMemoryPublishOnly);
        else
            await draftRepository.UpdateAsync(draft, EventPublishOption.InMemoryPublishOnly);

        return Success();
    }
}

/// <summary>
/// C8 -- an explicit save of the SOAP note and the coded problem list (§8.4). The handler resolves each
/// diagnosis code to a snapshot (code, name, active) and the aggregate decides: <c>DiagnosisCodeInactive</c>,
/// <c>DuplicateProblem</c>, the limits. A stale <c>ExpectedVersion</c> (another vet saved first) is a
/// <c>409</c>; <c>xmin</c> at save settles a true race.
/// </summary>
public sealed class SaveConsultationNoteCommandHandler(
    IAggregateRootRepository<VisitAggregate> visitRepository,
    IAggregateRootRepository<DiagnosisCodeAggregate> codeRepository,
    IAggregateRootRepository<NoteTemplateAggregate> templateRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SaveConsultationNoteCommand>
{
    public async Task<CommandResponse> HandleAsync(SaveConsultationNoteCommand command)
    {
        var visit = await visitRepository.GetByIdAsync(command.VisitId);
        if (visit is null || visit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.VisitNotFound, nameof(command.VisitId), ClinicalMessages.VisitNotFound);

        // A finalised visit answers VisitFinalised (the aggregate), never a stale-version error.
        if (visit.Status == VisitStatus.Open && visit.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleVisitVersion, nameof(command.ExpectedVersion), ClinicalMessages.StaleVisit);

        if (command.AppliedTemplateId is { } templateId && !await templateRepository.ExistAsync(t => t.Id == templateId))
            return Violation(BusinessViolationCodes.NoteTemplateNotFound, nameof(command.AppliedTemplateId), "Note template not found.", nameof(NoteTemplateAggregate));

        var problems = new List<ProblemDto>(command.Problems.Count);
        foreach (var input in command.Problems)
        {
            var code = await codeRepository.GetByIdAsync(input.DiagnosisCodeId);
            if (code is null)
                return Violation(BusinessViolationCodes.DiagnosisCodeNotFound, nameof(input.DiagnosisCodeId), "Diagnosis code not found.");

            problems.Add(new ProblemDto(
                input.ProblemId, code.Id, code.Code, code.Name, code.IsActive, AppointmentMapper.ParseEnum<ProblemCertainty>(input.Certainty), input.Note));
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            visit.SaveNote(new SoapNoteDto(
                command.Subjective, command.Objective, command.Assessment, command.Plan, command.DischargeInstructions, problems,
                command.AppliedTemplateId, command.AppliedTemplateVersion, command.UserContext.UserId, now, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await visitRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);
        return Success(new SaveNoteResult(visit.Version, now));
    }
}

/// <summary>
/// C9 -- finalises the visit (§8.4, US-CLN-17, US-CLN-22). Sequence: load, branch rule, version check,
/// <c>Finalise</c> with the caller's roles (read from the caller's own staff account -- the aggregate
/// re-checks <c>Vet</c>, ADR-119), enqueue <c>visit.summary-available</c> to the cat's <i>current</i> owner
/// (HO-NTF-001 ADR-056: in the handler, before the save, never from an event handler), save. The message is
/// link-only: no clinical content travels in it (ADR-130). Finalising does <b>not</b> complete the
/// appointment (ADR-133).
/// </summary>
public sealed class FinaliseVisitCommandHandler(
    IAggregateRootRepository<VisitAggregate> visitRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    INotificationOutbox outbox,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<FinaliseVisitCommand>
{
    public async Task<CommandResponse> HandleAsync(FinaliseVisitCommand command)
    {
        var visit = await visitRepository.GetByIdAsync(command.VisitId);
        if (visit is null || visit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.VisitNotFound, nameof(command.VisitId), ClinicalMessages.VisitNotFound);

        // A second finalise is VisitFinalised (the aggregate), not a stale-version error: check that first.
        if (visit.Status == VisitStatus.Open && visit.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleVisitVersion, nameof(command.ExpectedVersion), ClinicalMessages.StaleVisit);

        var caller = await staffRepository.GetByIdAsync(command.UserContext.UserId);
        IReadOnlyList<string> roles = caller?.RoleIds ?? [];

        // The cat's owner NOW is the customer the visit is billed to (HO-BIL-001 Phase J, owner decision 2026-10-14). The aggregate stores it and
        // refuses (CatNotFound) when there is none -- Billing never guesses a customer.
        var cat = await catRepository.GetByIdAsync(visit.CatId);

        var now = timeProvider.GetUtcNow();
        try
        {
            visit.Finalise(new VisitFinalisationDto(roles, command.UserContext.UserId, now, command.UserContext, command.CorrelationId, cat?.OwnerId ?? Guid.Empty));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (cat is not null)
        {
            var branch = await branchRepository.GetByIdAsync(visit.BranchId);
            var visitDate = BranchCalendar.LocalDateOf(visit.OpenedOn, branch?.TimeZoneId ?? "UTC");

            // Variables carry the cat's name, a date and a link -- never a diagnosis, a vet or a note (ADR-130).
            outbox.Enqueue(new NotificationRequestDto(
                Guid.NewGuid(), NotificationTemplateCatalogue.VisitSummaryAvailable, NotificationRecipientKind.Customer, cat.OwnerId,
                new Dictionary<string, string>
                {
                    ["catName"] = cat.Name,
                    ["visitDate"] = visitDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["summaryLink"] = linkBuilder.VisitSummaryLink(visit.Id),
                },
                BranchId: visit.BranchId, CorrelationId: command.CorrelationId, UserContext: command.UserContext));
        }

        await visitRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// C10 -- an addendum to a finalised visit (§8.4, US-CLN-17 AC2). A separate root: the original text stays
/// exactly as finalised. The visit is only read; the aggregate refuses an addendum on an open visit.
/// </summary>
public sealed class AddVisitAddendumCommandHandler(
    IAggregateRootRepository<VisitAggregate> visitRepository,
    IAggregateRootRepository<VisitAddendumAggregate> addendumRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AddVisitAddendumCommand>
{
    public async Task<CommandResponse> HandleAsync(AddVisitAddendumCommand command)
    {
        var visit = await visitRepository.GetByIdAsync(command.VisitId);
        if (visit is null || visit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.VisitNotFound, nameof(command.VisitId), ClinicalMessages.VisitNotFound);

        var addendum = new VisitAddendumAggregate(command.AddendumId);
        try
        {
            addendum.Add(command.ToDomainDto(visit.CatId, visit.Status, timeProvider.GetUtcNow()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(VisitAddendumAggregate));
        }

        await addendumRepository.SaveAsync(addendum, EventPublishOption.InMemoryPublishOnly);
        return Success(addendum.Id);
    }
}

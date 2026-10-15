using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Cat;

/// <summary>What <see cref="NeuterStatusApplication"/> did, for the event handler (which records it) and the repair command (which answers it).</summary>
public enum NeuterStatusOutcome
{
    /// <summary>The cat is now recorded Neutered on the surgery's local date.</summary>
    Applied,

    /// <summary>It already was, on that date: nothing changed (idempotent).</summary>
    AlreadyApplied,

    /// <summary>The cat is deceased: no change; the reason is <c>CatIsDeceased</c>.</summary>
    CatDeceased,

    /// <summary>HO-CAT-001's own invariant refused the date (e.g. before the date of birth); <see cref="NeuterStatusResult.ViolationCode"/> says which.</summary>
    Refused,
}

public sealed record NeuterStatusResult(NeuterStatusOutcome Outcome, string? ViolationCode, string? Message);

/// <summary>
/// HO-SRG-001 §8.14 (US-SRG-06, ADR-166) -- the one place a completed sterilisation reaches the cat. It lives in the CAT context (`DomainEventHandlers/Cat/`) and uses only the cat's own
/// <c>RecordNeuterStatus</c> -- the surgery never writes <c>clinic.cat</c> (ADR-166, .ai/flows/surgery-flow.md). Idempotent. Shared by the event handler and by the repair command
/// (<c>ApplySterilisationOutcomeCommand</c>) so the two can never disagree.
/// </summary>
public static class NeuterStatusApplication
{
    public static async Task<NeuterStatusResult> ApplyAsync(
        IAggregateRootRepository<CatAggregate> catRepository, ICatRecordClock catClock, Guid catId, DateOnly completedOnLocalDate, UserContext user, Guid correlationId)
    {
        var cat = await catRepository.GetByIdAsync(catId);
        if (cat is null) return new NeuterStatusResult(NeuterStatusOutcome.Refused, BusinessViolationCodes.CatNotFound, "The cat could not be found.");

        if (cat.LifeStatus != CatLifeStatus.Alive)
            return new NeuterStatusResult(NeuterStatusOutcome.CatDeceased, BusinessViolationCodes.CatIsDeceased, "The cat is recorded as deceased, so its neuter status was not changed.");

        if (cat.NeuterStatus == NeuterStatus.Neutered && cat.NeuteredOn == completedOnLocalDate)
            return new NeuterStatusResult(NeuterStatusOutcome.AlreadyApplied, null, null);

        // The cat's own "today" is the main branch's date; a branch whose local date is already ahead of it must not have a real procedure date refused as "in the future".
        var today = await catClock.LocalTodayAsync(CancellationToken.None);
        if (completedOnLocalDate > today) today = completedOnLocalDate;

        try
        {
            cat.RecordNeuterStatus(new CatNeuterStatusDto(NeuterStatus.Neutered, completedOnLocalDate, today, user, correlationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return new NeuterStatusResult(NeuterStatusOutcome.Refused, ex.ErrorCode, ex.Message);
        }

        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);
        return new NeuterStatusResult(NeuterStatusOutcome.Applied, null, null);
    }
}

/// <summary>
/// HO-SRG-001 §8.14 (US-SRG-06, ADR-166) -- a COMPLETED spay or neuter sets the cat's neuter status and date automatically (no manual entry). A deceased cat, or a date HO-CAT-001's own
/// invariants refuse, is NOT an error here: the surgery has already ended and is durable (a failing handler fails the request after the commit, domain-events-and-outbox.md section 3), so the
/// handler writes a <c>NeuterStatusNotApplied</c> audit row with the reason and returns. Infrastructure exceptions DO propagate. The repair path is <c>ApplySterilisationOutcomeCommand</c>.
/// </summary>
public sealed class SetNeuterStatusOnSterilisationHandler(
    ILogger<SetNeuterStatusOnSterilisationHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    ICatRecordClock catClock,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<SterilisationCompletedEvent>
{
    public async Task HandleAsync(SterilisationCompletedEvent @event)
    {
        var result = await NeuterStatusApplication.ApplyAsync(catRepository, catClock, @event.CatId, @event.CompletedOnLocalDate, @event.UserContext, @event.CorrelationId);
        if (result.Outcome is NeuterStatusOutcome.Applied or NeuterStatusOutcome.AlreadyApplied) return;

        logger.LogInformation(
            "Neuter status of cat {CatId} not applied after surgery {SurgeryId}: {Reason}. CorrelationId: {CorrelationId}", @event.CatId, @event.SurgeryId, result.ViolationCode, @event.CorrelationId);

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "Surgery", EntityId: @event.SurgeryId, Action: "NeuterStatusNotApplied", Reason: result.Outcome == NeuterStatusOutcome.CatDeceased ? "CatDeceased" : result.ViolationCode ?? "Refused",
            ActorUserId: @event.UserContext.UserId, OccurredOn: timeProvider.GetUtcNow(), BeforeValue: null, AfterValue: $"CatId={@event.CatId};CompletedOn={@event.CompletedOnLocalDate:yyyy-MM-dd}",
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId, Category: AuditCategory.BusinessEvent, IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}

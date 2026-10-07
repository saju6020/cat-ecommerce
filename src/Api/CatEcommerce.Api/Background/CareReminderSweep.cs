using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-NTF-001 §8.12 / ADR-065 -- the second timed producer in the worker. Run hourly by
/// <see cref="NotificationWorker"/> (via <see cref="NotificationWorkCycle"/>). A window, not an
/// exact date, so a missed hour still fires the next one; the deduplication key means it fires once.
/// </summary>
public sealed class CareReminderSweep(
    ILogger<CareReminderSweep> logger,
    IInMemoryDispatcher dispatcher,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IBusinessSettings businessSettings,
    INotificationLinkBuilder linkBuilder,
    TimeProvider timeProvider)
{
    private static UserContext WorkerContext => new(SystemActors.NotificationWorker, Guid.Empty);

    public async Task RunAsync(CancellationToken ct = default)
    {
        var mainBranch = await branchRepository.GetByIdAsync(AdministrationSeed.MainBranchId);
        var timeZoneId = mainBranch?.TimeZoneId ?? "Asia/Dhaka";
        var today = Domain.DomainServices.BranchCalendar.LocalDateOf(timeProvider.GetUtcNow(), timeZoneId);

        var vaccinationLeadDays = businessSettings.GetInt(SettingKeys.NotificationsVaccinationReminderLeadDays);
        var dewormingLeadDays = businessSettings.GetInt(SettingKeys.NotificationsDewormingReminderLeadDays);

        var queryResponse = await dispatcher.GetResultAsync(new GetDueCareRemindersQuery
        {
            Today = today, VaccinationLeadDays = vaccinationLeadDays, DewormingLeadDays = dewormingLeadDays,
            UserContext = WorkerContext, CorrelationId = Guid.NewGuid()
        });

        foreach (var row in queryResponse.Result ?? [])
        {
            try
            {
                await RequestReminderAsync(row);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Care reminder sweep failed for cat {CatId}, kind {Kind}.", row.CatId, row.Kind);
            }
        }
    }

    private async Task RequestReminderAsync(DueCareReminderRow row)
    {
        var isVaccination = row.Kind == "Vaccination";
        var templateKey = isVaccination ? NotificationTemplateCatalogue.VaccinationDue : NotificationTemplateCatalogue.DewormingDue;
        var subjectKind = isVaccination ? NotificationSubjectKind.CatVaccinationDue : NotificationSubjectKind.CatDewormingDue;
        var dedupKind = isVaccination ? "vaccination" : "deworming";

        var variables = new Dictionary<string, string>
        {
            ["catName"] = row.CatName,
            [isVaccination ? "vaccineName" : "treatmentName"] = row.ReferenceName,
            ["dueOn"] = row.DueOn.ToString("yyyy-MM-dd"),
            // HO-APT-001 §8.9 amendment: CatCareLink now takes the booking category to preselect --
            // ServiceCategory.Vaccination for a vaccination reminder, ServiceCategory.FollowUp for
            // a deworming/parasite one (there is no dedicated "Deworming" service category).
            ["bookingLink"] = linkBuilder.CatCareLink(row.CatId, isVaccination ? "Vaccination" : "FollowUp")
        };

        var command = new RequestNotificationCommand
        {
            RequestId = Guid.NewGuid(),
            TemplateKey = templateKey,
            RecipientKind = NotificationRecipientKind.Customer.DisplayName,
            RecipientId = row.OwnerId,
            Variables = variables,
            SubjectKind = subjectKind.DisplayName,
            SubjectId = row.CatId,
            SubjectRef = row.ReferenceId,
            SubjectDueOn = row.DueOn,
            // TEST gate fix 2026-09-27 (MEDIUM, I-F5/A6): the key did not vary by owner, and
            // RequestNotificationCommandHandler's dedup pre-check matches ANY existing row with
            // the key regardless of status -- after a cat transfer, the original owner's request
            // is correctly Suppressed (SubjectNoLongerApplies), but that already consumed the key,
            // so the identical key computed for the new owner (same cat, same vaccine/treatment,
            // same NextDueOn -- a transfer touches none of those) was silently treated as
            // "already requested" and the new owner never got a reminder for that due date, for as
            // long as a year (until the vaccine's own NextDueOn next changed). Including the owner
            // means a transfer computes a genuinely new key, so the new owner's sweep is never
            // blocked by the original owner's now-suppressed request.
            // NTF-001 backlog row 1: an A->B->A round trip within one due window reused A's first
            // (Suppressed) key. The cat's ownership generation (0 until first change, so existing
            // keys are unchanged) is appended from the first change onward.
            DeduplicationKey = $"care:{dedupKind}:{row.CatId:N}:{row.ReferenceId:N}:{row.OwnerId:N}:{row.DueOn:yyyyMMdd}"
                + (row.OwnershipGeneration > 0 ? $":g{row.OwnershipGeneration}" : ""),
            UserContext = WorkerContext,
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        await dispatcher.SendAsync(command);
    }
}

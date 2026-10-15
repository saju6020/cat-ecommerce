using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Prescription.PharmacyHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Prescription;

/// <summary>
/// HO-PHM-001 §8.10 / §11.4 C17 (US-PHM-08 AC1, BR-2, BR-6; ADR-149) -- a vet authorises a named therapeutic food for ONE cat from an OPEN visit. Vet only (the filter refuses everyone else
/// with a logged 403; this checks again). The product must be an active HO-SHP-001 product that requires a prescription (<c>ProductNotPrescriptionDiet</c> otherwise); a cat can hold one
/// active authorisation per product (<c>DietAuthorisationAlreadyActive</c> -- extend it instead; the filtered unique index is the race guard and is mapped to the same 400). The owner is
/// told through the outbox, enqueued here, before the save.
/// </summary>
public sealed class AuthoriseDietCommandHandler(
    IAggregateRootRepository<DietAuthorisationAggregate> repository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<VisitAggregate> visitRepository,
    ICatalogueReader catalogue,
    IPharmacyClock clock,
    IBusinessSettings settings,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AuthoriseDietCommand>
{
    public async Task<CommandResponse> HandleAsync(AuthoriseDietCommand command)
    {
        var ct = CancellationToken.None;
        var user = command.UserContext!;

        var caller = await staffRepository.GetByIdAsync(user.UserId);
        if (caller is null || !caller.RoleIds.Contains(StaffRoles.Vet))
            return Violation(BusinessViolationCodes.PrescribingNotPermitted, nameof(command.UserContext), "Only a veterinarian may authorise a therapeutic diet.");

        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null) return Violation(BusinessViolationCodes.CatNotFound, nameof(command.CatId), "Cat not found.");
        if (cat.LifeStatus != CatLifeStatus.Alive) return Violation(BusinessViolationCodes.CatNotTreatable, nameof(command.CatId), $"{cat.Name} is no longer with us.");

        var visit = await visitRepository.GetByIdAsync(command.VisitId);
        if (visit is null || visit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.VisitNotFound, nameof(command.VisitId), "Visit not found.");
        if (visit.CatId != command.CatId)
            return Violation(BusinessViolationCodes.VisitCatMismatch, nameof(command.VisitId), "That visit belongs to a different cat.");
        if (visit.Status != VisitStatus.Open)
            return Violation(BusinessViolationCodes.VisitNotOpen, nameof(command.VisitId), "A diet can only be authorised from an open visit.");

        var product = await catalogue.GetProductAsync(command.ProductId, ct);
        if (product is null || !product.IsActive)
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), "That product was not found.");
        if (!product.RequiresPrescription)
            return Violation(BusinessViolationCodes.ProductNotPrescriptionDiet, nameof(command.ProductId), "That product does not need a prescription.");

        var catId = command.CatId;
        var productId = command.ProductId;
        if (await repository.ExistAsync(a => a.CatId == catId && a.ProductId == productId && a.Status == DietAuthorisationStatus.Active))
            return Violation(BusinessViolationCodes.DietAuthorisationAlreadyActive, nameof(command.ProductId), "This cat already has an active authorisation for that food. Extend it instead.");

        var authorisation = new DietAuthorisationAggregate(command.AuthorisationId);
        try
        {
            authorisation.Authorise(new DietAuthorisationDto(
                cat.Id, cat.OwnerId, product.ProductId, product.Name, visit.Id, user.UserId, command.ValidFrom, command.ValidTo, command.Notes, await clock.MainBranchTodayAsync(ct),
                settings.GetInt(SettingKeys.PharmacyMaxDietAuthorisationDays), timeProvider.GetUtcNow(), command.ToPharmacyActor()));
        }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        PharmacyNotifications.EnqueueDietAuthorised(outbox, links, cat.OwnerId, cat.Name, product.Name, authorisation.ValidTo, command.CorrelationId, user);

        await repository.SaveAsync(authorisation, EventPublishOption.InMemoryPublishOnly);
        return Success(new AuthorisedDiet(authorisation.Id));
    }
}

/// <summary>HO-PHM-001 §11.4 C18 -- extend an active authorisation, or one that ended within the last 30 days (a renewal after a short lapse re-activates it). Vet only.</summary>
public sealed class ExtendDietAuthorisationCommandHandler(
    IAggregateRootRepository<DietAuthorisationAggregate> repository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IPharmacyClock clock,
    IBusinessSettings settings)
    : ICommandHandlerAsync<ExtendDietAuthorisationCommand>
{
    public async Task<CommandResponse> HandleAsync(ExtendDietAuthorisationCommand command)
    {
        var caller = await staffRepository.GetByIdAsync(command.UserContext!.UserId);
        if (caller is null || !caller.RoleIds.Contains(StaffRoles.Vet))
            return Violation(BusinessViolationCodes.PrescribingNotPermitted, nameof(command.UserContext), "Only a veterinarian may extend a diet authorisation.");

        var authorisation = await repository.GetByIdAsync(command.AuthorisationId);
        if (authorisation is null)
            return Violation(BusinessViolationCodes.DietAuthorisationNotFound, nameof(command.AuthorisationId), "Authorisation not found.");

        try
        {
            authorisation.Extend(command.ValidTo, await clock.MainBranchTodayAsync(CancellationToken.None), settings.GetInt(SettingKeys.PharmacyMaxDietAuthorisationDays), command.ToPharmacyActor());
        }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        await repository.UpdateAsync(authorisation, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>HO-PHM-001 §11.4 C18 -- revoke an active authorisation with a reason. The gate stops saying yes at once. Vet only.</summary>
public sealed class RevokeDietAuthorisationCommandHandler(
    IAggregateRootRepository<DietAuthorisationAggregate> repository, IAggregateRootRepository<StaffAccountAggregate> staffRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<RevokeDietAuthorisationCommand>
{
    public async Task<CommandResponse> HandleAsync(RevokeDietAuthorisationCommand command)
    {
        var user = command.UserContext!;
        var caller = await staffRepository.GetByIdAsync(user.UserId);
        if (caller is null || !caller.RoleIds.Contains(StaffRoles.Vet))
            return Violation(BusinessViolationCodes.PrescribingNotPermitted, nameof(command.UserContext), "Only a veterinarian may revoke a diet authorisation.");

        var authorisation = await repository.GetByIdAsync(command.AuthorisationId);
        if (authorisation is null)
            return Violation(BusinessViolationCodes.DietAuthorisationNotFound, nameof(command.AuthorisationId), "Authorisation not found.");

        try { authorisation.Revoke(user.UserId, command.Reason, timeProvider.GetUtcNow(), command.ToPharmacyActor()); }
        catch (DomainRuleViolationException ex) { return Violation(ex); }

        await repository.UpdateAsync(authorisation, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// HO-PHM-001 §8.10 / §11.4 C19 (US-PHM-08 AC2, ADR-149) -- the worker records an expiry: <c>Expire</c> (raising <c>DietAuthorisationExpiredEvent</c> for HO-SUB-001) and a message to the cat's
/// CURRENT owner, if the cat is alive -- enqueued here because this IS a command with a business change (HO-NTF-001 rule 1), deduplicated per authorisation. Idempotent: not expirable
/// (already expired, revoked, still in date) is success with nothing written. The product was locked by the gate the moment the date passed; this is the record of it.
/// </summary>
public sealed class ExpireDietAuthorisationCommandHandler(
    IAggregateRootRepository<DietAuthorisationAggregate> repository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IPharmacyClock clock,
    INotificationOutbox outbox,
    INotificationLinkBuilder links)
    : ICommandHandlerAsync<ExpireDietAuthorisationCommand>
{
    public async Task<CommandResponse> HandleAsync(ExpireDietAuthorisationCommand command)
    {
        var authorisation = await repository.GetByIdAsync(command.AuthorisationId);
        if (authorisation is null) return Success();

        var today = await clock.MainBranchTodayAsync(CancellationToken.None);
        if (!authorisation.CanExpire(today)) return Success();

        authorisation.Expire(today, command.ToPharmacyActor());

        if (await catRepository.GetByIdAsync(authorisation.CatId) is { } cat && cat.LifeStatus == CatLifeStatus.Alive)
        {
            PharmacyNotifications.EnqueueDietAuthorisationExpired(
                outbox, links, cat.OwnerId, cat.Id, cat.Name, authorisation.ProductNameSnapshot, authorisation.ValidTo, authorisation.Id, command.CorrelationId, command.UserContext!);
        }

        await repository.UpdateAsync(authorisation, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

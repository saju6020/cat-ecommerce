using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Surgery.SurgeryHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Surgery;

/// <summary>Turns a campaign command's price lines into domain input: the service must exist, and whether it is a sterilisation is the service's own category (the aggregate refuses the rest).</summary>
internal static class SurgeryCampaignSupport
{
    public static async Task<(IReadOnlyList<CampaignPriceInput>? Prices, CommandResponse? Failure)> BuildPricesAsync(
        IAggregateRootRepository<ClinicServiceAggregate> serviceRepository, IBusinessSettings settings, IReadOnlyList<SurgeryCampaignPriceInput> lines)
    {
        var currency = settings.GetString(SettingKeys.CommerceCurrencyCode);
        var prices = new List<CampaignPriceInput>();
        foreach (var line in lines)
        {
            var service = await serviceRepository.GetByIdAsync(line.ServiceId);
            if (service is null)
                return (null, Violation(BusinessViolationCodes.CampaignServiceNotSterilisation, "Prices", "A priced procedure does not exist.", "SurgeryCampaignAggregate"));

            var isSterilisation = service.Category == ServiceCategory.Spay || service.Category == ServiceCategory.Neuter;
            prices.Add(new CampaignPriceInput(service.Id, isSterilisation, new Money(line.Amount, currency)));
        }

        return (prices, null);
    }
}

/// <summary>HO-SRG-001 section 11.4 C16 (US-SRG-11, ADR-171) -- create a campaign. The code is unique; every price is for a sterilisation service (the aggregate enforces it).</summary>
public sealed class CreateSurgeryCampaignCommandHandler(
    IAggregateRootRepository<SurgeryCampaignAggregate> campaignRepository,
    IAggregateRootRepository<ClinicServiceAggregate> serviceRepository,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CreateSurgeryCampaignCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateSurgeryCampaignCommand command)
    {
        var code = command.Code.Trim().ToUpperInvariant();
        if (await campaignRepository.GetByFilterAsync(c => c.Code == code) is not null)
            return Violation(BusinessViolationCodes.CampaignCodeAlreadyExists, nameof(command.Code), "A campaign with this code already exists.", "SurgeryCampaignAggregate");

        var (prices, failure) = await SurgeryCampaignSupport.BuildPricesAsync(serviceRepository, businessSettings, command.Prices);
        if (failure is not null) return failure;

        var campaign = new SurgeryCampaignAggregate(Guid.NewGuid());
        try
        {
            campaign.Create(new SurgeryCampaignDto(
                command.Code, command.Name, command.Description, command.ValidFrom, command.ValidTo, command.BranchIds, prices!, command.MaxCases,
                timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex, "SurgeryCampaignAggregate");
        }

        await campaignRepository.SaveAsync(campaign, EventPublishOption.InMemoryPublishOnly);
        return Success(campaign.Id);
    }
}

/// <summary>C16 -- change a campaign. The stored count of surgeries already booked under it decides the price lock and the cap floor.</summary>
public sealed class UpdateSurgeryCampaignCommandHandler(
    IAggregateRootRepository<SurgeryCampaignAggregate> campaignRepository,
    IAggregateRootRepository<ClinicServiceAggregate> serviceRepository,
    ISurgeryFinder surgeryFinder,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<UpdateSurgeryCampaignCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateSurgeryCampaignCommand command)
    {
        var campaign = await campaignRepository.GetByIdAsync(command.CampaignId);
        if (campaign is null)
            return Violation(BusinessViolationCodes.SurgeryCampaignNotFound, nameof(command.CampaignId), "Campaign not found.", "SurgeryCampaignAggregate");
        if (campaign.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleVersion, nameof(command.ExpectedVersion), "The campaign has changed since you loaded it. Reload and try again.", "SurgeryCampaignAggregate");

        var (prices, failure) = await SurgeryCampaignSupport.BuildPricesAsync(serviceRepository, businessSettings, command.Prices);
        if (failure is not null) return failure;

        var booked = await surgeryFinder.CountForCampaignAsync(campaign.Id);
        try
        {
            campaign.Update(new SurgeryCampaignDto(
                campaign.Code, command.Name, command.Description, command.ValidFrom, command.ValidTo, command.BranchIds, prices!, command.MaxCases,
                timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId), booked);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex, "SurgeryCampaignAggregate");
        }

        await campaignRepository.UpdateAsync(campaign, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class SetSurgeryCampaignActiveCommandHandler(IAggregateRootRepository<SurgeryCampaignAggregate> campaignRepository)
    : ICommandHandlerAsync<SetSurgeryCampaignActiveCommand>
{
    public async Task<CommandResponse> HandleAsync(SetSurgeryCampaignActiveCommand command)
    {
        var campaign = await campaignRepository.GetByIdAsync(command.CampaignId);
        if (campaign is null)
            return Violation(BusinessViolationCodes.SurgeryCampaignNotFound, nameof(command.CampaignId), "Campaign not found.", "SurgeryCampaignAggregate");

        campaign.SetActive(command.IsActive, command.UserContext!, command.CorrelationId);

        await campaignRepository.UpdateAsync(campaign, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

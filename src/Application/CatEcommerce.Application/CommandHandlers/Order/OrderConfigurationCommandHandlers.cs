using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Order.OrderHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>HO-ORD-001 §11.4 C1 -- create a delivery zone (US-ORD-06). Uniqueness of the name and the branch's existence are repository checks in
/// the handler (the unique index is the race backstop); everything about the zone's own shape is the aggregate's.</summary>
public sealed class CreateDeliveryZoneCommandHandler(
    IAggregateRootRepository<DeliveryZoneAggregate> zones,
    IAggregateRootRepository<BranchAggregate> branches,
    IBusinessSettings settings)
    : ICommandHandlerAsync<CreateDeliveryZoneCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateDeliveryZoneCommand command)
    {
        var branch = await branches.GetByIdAsync(command.BranchId);
        if (branch is null || !branch.IsActive)
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var name = command.Name.Trim();
        if (await zones.ExistAsync(z => z.Name == name))
            return Violation(BusinessViolationCodes.DeliveryZoneNameAlreadyExists, nameof(command.Name), OrderMessages.DeliveryZoneNameAlreadyExists);

        var zone = new DeliveryZoneAggregate(command.ZoneId);
        try
        {
            zone.Create(command.ToDomainDto(settings.GetString(SettingKeys.CommerceCurrencyCode, command.BranchId)));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await zones.SaveAsync(zone, EventPublishOption.InMemoryPublishOnly);
        return Success(zone.Id);
    }
}

/// <summary>C1 -- edit a delivery zone in place.</summary>
public sealed class UpdateDeliveryZoneCommandHandler(
    IAggregateRootRepository<DeliveryZoneAggregate> zones,
    IAggregateRootRepository<BranchAggregate> branches,
    IBusinessSettings settings)
    : ICommandHandlerAsync<UpdateDeliveryZoneCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateDeliveryZoneCommand command)
    {
        var zone = await zones.GetByIdAsync(command.ZoneId);
        if (zone is null)
            return Violation(BusinessViolationCodes.DeliveryZoneNotFound, nameof(command.ZoneId), "Delivery zone not found.");

        var branch = await branches.GetByIdAsync(command.BranchId);
        if (branch is null || !branch.IsActive)
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var name = command.Name.Trim();
        if (await zones.ExistAsync(z => z.Name == name && z.Id != command.ZoneId))
            return Violation(BusinessViolationCodes.DeliveryZoneNameAlreadyExists, nameof(command.Name), OrderMessages.DeliveryZoneNameAlreadyExists);

        try
        {
            zone.Update(command.ToDomainDto(settings.GetString(SettingKeys.CommerceCurrencyCode, command.BranchId)));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await zones.UpdateAsync(zone, EventPublishOption.InMemoryPublishOnly);
        return Success(zone.Id);
    }
}

public sealed class SetDeliveryZoneActivationCommandHandler(IAggregateRootRepository<DeliveryZoneAggregate> zones)
    : ICommandHandlerAsync<SetDeliveryZoneActivationCommand>
{
    public async Task<CommandResponse> HandleAsync(SetDeliveryZoneActivationCommand command)
    {
        var zone = await zones.GetByIdAsync(command.ZoneId);
        if (zone is null)
            return Violation(BusinessViolationCodes.DeliveryZoneNotFound, nameof(command.ZoneId), "Delivery zone not found.");

        zone.SetActivation(command.IsActive, command.UserContext!, command.CorrelationId);
        await zones.UpdateAsync(zone, EventPublishOption.InMemoryPublishOnly);
        return Success(zone.Id);
    }
}

/// <summary>C2 -- create a courier. The name is unique (pre-check here, unique index behind it).</summary>
public sealed class CreateCourierCommandHandler(IAggregateRootRepository<CourierAggregate> couriers) : ICommandHandlerAsync<CreateCourierCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateCourierCommand command)
    {
        var name = command.Name.Trim();
        if (await couriers.ExistAsync(c => c.Name == name))
            return Violation(BusinessViolationCodes.CourierNameAlreadyExists, nameof(command.Name), OrderMessages.CourierNameAlreadyExists);

        var courier = new CourierAggregate(command.CourierId);
        try
        {
            courier.Create(command.Name, command.TrackingUrlTemplate, command.SupportsCod, command.IsActive, command.UserContext!);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await couriers.SaveAsync(courier, EventPublishOption.InMemoryPublishOnly);
        return Success(courier.Id);
    }
}

public sealed class UpdateCourierCommandHandler(IAggregateRootRepository<CourierAggregate> couriers) : ICommandHandlerAsync<UpdateCourierCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateCourierCommand command)
    {
        var courier = await couriers.GetByIdAsync(command.CourierId);
        if (courier is null)
            return Violation(BusinessViolationCodes.CourierNotFound, nameof(command.CourierId), "Courier not found.");

        var name = command.Name.Trim();
        if (await couriers.ExistAsync(c => c.Name == name && c.Id != command.CourierId))
            return Violation(BusinessViolationCodes.CourierNameAlreadyExists, nameof(command.Name), OrderMessages.CourierNameAlreadyExists);

        try
        {
            courier.Update(command.Name, command.TrackingUrlTemplate, command.SupportsCod, command.IsActive);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await couriers.UpdateAsync(courier, EventPublishOption.InMemoryPublishOnly);
        return Success(courier.Id);
    }
}

/// <summary>C3 -- set the one bank-transfer account. A first write creates the singleton (a race on its fixed id is a <c>409</c>).</summary>
public sealed class SetBankTransferAccountCommandHandler(IAggregateRootRepository<BankTransferAccountAggregate> accounts)
    : ICommandHandlerAsync<SetBankTransferAccountCommand>
{
    public async Task<CommandResponse> HandleAsync(SetBankTransferAccountCommand command)
    {
        var account = await accounts.GetByIdAsync(BankTransferAccountAggregate.SingletonId);
        var isNew = account is null;
        account ??= new BankTransferAccountAggregate(BankTransferAccountAggregate.SingletonId);

        try
        {
            if (isNew) account.Create(command.AccountName, command.AccountNumber, command.BankName, command.BranchName, command.RoutingNumber, command.IsActive, command.UserContext!);
            else account.Update(command.AccountName, command.AccountNumber, command.BankName, command.BranchName, command.RoutingNumber, command.IsActive);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (isNew) await accounts.SaveAsync(account, EventPublishOption.InMemoryPublishOnly);
        else await accounts.UpdateAsync(account, EventPublishOption.InMemoryPublishOnly);
        return Success(account.Id);
    }
}

/// <summary>C4 -- create a discount code. The text is unique (pre-check, unique index behind it) and every named product must exist in Shop.</summary>
public sealed class CreateDiscountCodeCommandHandler(
    IAggregateRootRepository<DiscountCodeAggregate> codes, ICatalogueLookup catalogue, IBusinessSettings settings)
    : ICommandHandlerAsync<CreateDiscountCodeCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateDiscountCodeCommand command)
    {
        var normalised = DiscountCodeAggregate.Normalise(command.Code);
        if (await codes.ExistAsync(c => c.Code == normalised))
            return Violation(BusinessViolationCodes.DiscountCodeAlreadyExists, nameof(command.Code), OrderMessages.DiscountCodeAlreadyExists);

        if (await HasMissingProductAsync(catalogue, command.ProductIds))
            return Violation(BusinessViolationCodes.InvalidDiscountDefinition, nameof(command.ProductIds), "A listed product does not exist.");

        var code = new DiscountCodeAggregate(command.DiscountCodeId);
        try
        {
            code.Create(command.ToDomainDto(settings.GetString(SettingKeys.CommerceCurrencyCode)));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await codes.SaveAsync(code, EventPublishOption.InMemoryPublishOnly);
        return Success(code.Id);
    }

    internal static async Task<bool> HasMissingProductAsync(ICatalogueLookup catalogue, IReadOnlyList<Guid> productIds)
    {
        if (productIds.Count == 0) return false;

        var distinct = productIds.Distinct().ToList();
        var existing = await catalogue.GetExistingProductIdsAsync(distinct, default);
        return distinct.Any(id => !existing.Contains(id));
    }
}

public sealed class UpdateDiscountCodeCommandHandler(
    IAggregateRootRepository<DiscountCodeAggregate> codes, ICatalogueLookup catalogue, IBusinessSettings settings)
    : ICommandHandlerAsync<UpdateDiscountCodeCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateDiscountCodeCommand command)
    {
        var code = await codes.GetByIdAsync(command.DiscountCodeId);
        if (code is null)
            return Violation(BusinessViolationCodes.DiscountCodeNotFound, nameof(command.DiscountCodeId), "Discount code not found.");

        if (await CreateDiscountCodeCommandHandler.HasMissingProductAsync(catalogue, command.ProductIds))
            return Violation(BusinessViolationCodes.InvalidDiscountDefinition, nameof(command.ProductIds), "A listed product does not exist.");

        try
        {
            code.Update(command.ToDomainDto(code.Code, settings.GetString(SettingKeys.CommerceCurrencyCode)));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await codes.UpdateAsync(code, EventPublishOption.InMemoryPublishOnly);
        return Success(code.Id);
    }
}

public sealed class SetDiscountCodeActivationCommandHandler(IAggregateRootRepository<DiscountCodeAggregate> codes)
    : ICommandHandlerAsync<SetDiscountCodeActivationCommand>
{
    public async Task<CommandResponse> HandleAsync(SetDiscountCodeActivationCommand command)
    {
        var code = await codes.GetByIdAsync(command.DiscountCodeId);
        if (code is null)
            return Violation(BusinessViolationCodes.DiscountCodeNotFound, nameof(command.DiscountCodeId), "Discount code not found.");

        code.SetActivation(command.IsActive, command.UserContext!, command.CorrelationId);
        await codes.UpdateAsync(code, EventPublishOption.InMemoryPublishOnly);
        return Success(code.Id);
    }
}

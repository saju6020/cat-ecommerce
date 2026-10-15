using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Inventory;

/// <summary>HO-IMP-001 §11.4 C1. Code unique per branch (a repository pre-check; <c>ix_stock_location_branch_code</c> backs it, §12.5);
/// a second ACTIVE quarantine location in a branch is refused (<c>ix_stock_location_one_quarantine</c> backs that).</summary>
public sealed class CreateStockLocationCommandHandler(
    ILogger<CreateStockLocationCommandHandler> logger,
    IAggregateRootRepository<StockLocationAggregate> repository)
    : ICommandHandlerAsync<CreateStockLocationCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateStockLocationCommand command)
    {
        logger.LogInformation("Creating stock location at branch {BranchId}. CorrelationId: {CorrelationId}", command.BranchId, command.CorrelationId);

        var code = command.Code.Trim().ToUpperInvariant();
        if (await repository.ExistAsync(l => l.BranchId == command.BranchId && l.Code == code))
            return InventoryResponses.Fail(BusinessViolationCodes.StockLocationCodeAlreadyExists, nameof(command.Code), InventoryMessages.StockLocationCodeAlreadyExists);

        var kind = InventoryMapper.ParseEnum<StockLocationKind>(command.Kind);
        if (kind == StockLocationKind.Quarantine &&
            await repository.ExistAsync(l => l.BranchId == command.BranchId && l.Kind == StockLocationKind.Quarantine && l.IsActive))
            return InventoryResponses.Fail(BusinessViolationCodes.QuarantineLocationAlreadyExists, nameof(command.Kind), InventoryMessages.QuarantineLocationAlreadyExists);

        var location = new StockLocationAggregate(command.LocationId);
        try { location.Create(command.ToDomainDto()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.SaveAsync(location, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(command.LocationId);
    }
}

public sealed class UpdateStockLocationCommandHandler(
    ILogger<UpdateStockLocationCommandHandler> logger,
    IAggregateRootRepository<StockLocationAggregate> repository)
    : ICommandHandlerAsync<UpdateStockLocationCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateStockLocationCommand command)
    {
        logger.LogInformation("Renaming stock location {LocationId}. CorrelationId: {CorrelationId}", command.LocationId, command.CorrelationId);

        var location = await repository.GetByIdAsync(command.LocationId);
        if (location is null || location.BranchId != command.BranchId)
            return InventoryResponses.NotFound(BusinessViolationCodes.StockLocationNotFound, nameof(command.LocationId), "That location was not found.");

        try { location.Rename(command.Name, command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(location, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>Deactivation is refused while any position at the location holds stock (<c>StockLocationHasStock</c>, a read the handler makes);
/// reactivating a quarantine location is refused while the branch already has another active one.</summary>
public sealed class SetStockLocationActivationCommandHandler(
    ILogger<SetStockLocationActivationCommandHandler> logger,
    IAggregateRootRepository<StockLocationAggregate> repository,
    IStockPositionReader positions)
    : ICommandHandlerAsync<SetStockLocationActivationCommand>
{
    public async Task<CommandResponse> HandleAsync(SetStockLocationActivationCommand command)
    {
        logger.LogInformation("Setting stock location {LocationId} active={IsActive}. CorrelationId: {CorrelationId}", command.LocationId, command.IsActive, command.CorrelationId);

        var location = await repository.GetByIdAsync(command.LocationId);
        if (location is null || location.BranchId != command.BranchId)
            return InventoryResponses.NotFound(BusinessViolationCodes.StockLocationNotFound, nameof(command.LocationId), "That location was not found.");

        if (command.IsActive)
        {
            if (location.Kind == StockLocationKind.Quarantine && !location.IsActive &&
                await repository.ExistAsync(l => l.BranchId == location.BranchId && l.Kind == StockLocationKind.Quarantine && l.IsActive && l.Id != location.Id))
                return InventoryResponses.Fail(BusinessViolationCodes.QuarantineLocationAlreadyExists, nameof(command.IsActive), InventoryMessages.QuarantineLocationAlreadyExists);

            location.Reactivate(command.ToActor());
        }
        else
        {
            if (location.IsActive && await positions.LocationHasStockAsync(location.Id, CancellationToken.None))
                return InventoryResponses.Fail(BusinessViolationCodes.StockLocationHasStock, nameof(command.IsActive), "This location still holds stock. Move or write it off first.");

            location.Deactivate(command.ToActor());
        }

        await repository.UpdateAsync(location, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>HO-IMP-001 §11.4 C2. The product must exist (any SKU of it, through <see cref="ISkuDirectory"/>); the 1:1 profile is created on
/// the first write -- a first-write race is a 409 through the primary key (§12.5).</summary>
public sealed class SetProductStorageProfileCommandHandler(
    ILogger<SetProductStorageProfileCommandHandler> logger,
    IAggregateRootRepository<ProductStorageProfileAggregate> repository,
    ISkuDirectory skus)
    : ICommandHandlerAsync<SetProductStorageProfileCommand>
{
    public async Task<CommandResponse> HandleAsync(SetProductStorageProfileCommand command)
    {
        logger.LogInformation("Setting storage profile for product {ProductId}. CorrelationId: {CorrelationId}", command.ProductId, command.CorrelationId);

        if (!await skus.ProductExistsAsync(command.ProductId, CancellationToken.None))
            return InventoryResponses.Fail(BusinessViolationCodes.SkuNotFound, nameof(command.ProductId), "That product was not found.");

        var profile = await repository.GetByIdAsync(command.ProductId);
        var isNew = profile is null;
        profile ??= new ProductStorageProfileAggregate(command.ProductId);

        try { profile.Set(command.ToDomainDto()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        if (isNew) await repository.SaveAsync(profile, EventPublishOption.InMemoryPublishOnly);
        else await repository.UpdateAsync(profile, EventPublishOption.InMemoryPublishOnly);

        return InventoryResponses.Ok();
    }
}

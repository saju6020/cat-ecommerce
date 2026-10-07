using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>
/// HO-SHP-001 section 11.4 C14. The SKU must exist (and so its product); the wishlist is loaded or
/// created on the first write (a first-write race is a 409 through the primary key, section 12.5);
/// adding a SKU that is already there is a no-op success with nothing saved.
/// </summary>
public sealed class AddWishlistItemCommandHandler(
    ILogger<AddWishlistItemCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository,
    IAggregateRootRepository<WishlistAggregate> wishlistRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AddWishlistItemCommand>
{
    public async Task<CommandResponse> HandleAsync(AddWishlistItemCommand command)
    {
        logger.LogInformation("Adding SKU {SkuId} to a wishlist. CorrelationId: {CorrelationId}", command.SkuId, command.CorrelationId);

        var product = await productRepository.GetByFilterAsync(p => p.Skus.Any(s => s.Id == command.SkuId));
        if (product is null)
            return WishlistViolation(BusinessViolationCodes.SkuNotFound, nameof(command.SkuId), $"SKU '{command.SkuId}' was not found.");

        // Row 18: only a product on the storefront can be wished for. A draft or unlisted product reads exactly
        // like an unknown SKU, so a leaked SKU id confirms nothing and the wishlist never shows its details.
        if (product.Status != ProductStatus.Active || !product.ListedOnStorefront)
            return WishlistViolation(BusinessViolationCodes.SkuNotFound, nameof(command.SkuId), $"SKU '{command.SkuId}' was not found.");

        var customerId = command.UserContext.UserId;
        var wishlist = await wishlistRepository.GetByIdAsync(customerId);
        var isNew = wishlist is null;
        wishlist ??= new WishlistAggregate(customerId);

        bool changed;
        try
        {
            changed = wishlist.AddItem(command.SkuId, product.Id, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return WishlistViolation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (!changed) return new CommandResponse { IsSuccess = true };

        if (isNew)
            await wishlistRepository.SaveAsync(wishlist, EventPublishOption.InMemoryPublishOnly);
        else
            await wishlistRepository.UpdateAsync(wishlist, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true };
    }

    internal static CommandResponse WishlistViolation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(WishlistAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}

/// <summary>HO-SHP-001 section 11.4 C14 -- removing an absent SKU (or from no wishlist) is a no-op
/// success.</summary>
public sealed class RemoveWishlistItemCommandHandler(
    ILogger<RemoveWishlistItemCommandHandler> logger,
    IAggregateRootRepository<WishlistAggregate> wishlistRepository)
    : ICommandHandlerAsync<RemoveWishlistItemCommand>
{
    public async Task<CommandResponse> HandleAsync(RemoveWishlistItemCommand command)
    {
        logger.LogInformation("Removing SKU {SkuId} from a wishlist. CorrelationId: {CorrelationId}", command.SkuId, command.CorrelationId);

        var wishlist = await wishlistRepository.GetByIdAsync(command.UserContext.UserId);
        if (wishlist is null) return new CommandResponse { IsSuccess = true };

        if (!wishlist.RemoveItem(command.SkuId, command.UserContext, command.CorrelationId))
            return new CommandResponse { IsSuccess = true };

        await wishlistRepository.UpdateAsync(wishlist, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true };
    }
}

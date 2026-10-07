using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>HO-SHP-001 section 11.4 C6. SKU exists (any product); currency from
/// <c>IBusinessSettings</c>; duplicate EffectiveFrom pre-check, backed by the unique index.</summary>
public sealed class ScheduleSkuPriceCommandHandler(
    ILogger<ScheduleSkuPriceCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository,
    IAggregateRootRepository<SkuPriceAggregate> priceRepository,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ScheduleSkuPriceCommand>
{
    public async Task<CommandResponse> HandleAsync(ScheduleSkuPriceCommand command)
    {
        logger.LogInformation("Scheduling price for SKU {SkuId}. CorrelationId: {CorrelationId}", command.SkuId, command.CorrelationId);

        if (!await productRepository.ExistAsync(p => p.Skus.Any(s => s.Id == command.SkuId)))
            return Violation(BusinessViolationCodes.SkuNotFound, nameof(command.SkuId), $"SKU '{command.SkuId}' was not found.");

        if (await priceRepository.ExistAsync(p => p.SkuId == command.SkuId && p.EffectiveFrom == command.EffectiveFrom))
            return Violation(BusinessViolationCodes.DuplicatePriceEffectiveDate, nameof(command.EffectiveFrom), ShopMessages.DuplicatePriceEffectiveDate);

        var currencyCode = businessSettings.GetString(SettingKeys.CommerceCurrencyCode);
        var now = timeProvider.GetUtcNow();
        var price = new SkuPriceAggregate(command.SkuPriceId);

        try
        {
            price.Create(command.ToDomainDto(now), currencyCode);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await priceRepository.SaveAsync(price, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = price.Id };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(SkuPriceAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}

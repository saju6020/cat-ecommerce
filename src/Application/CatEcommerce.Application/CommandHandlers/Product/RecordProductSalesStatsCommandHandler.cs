using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Product;

/// <summary>
/// HO-SHP-001 section 11.4 C10. Product must exist; load-or-create the copy; Apply; save only if
/// changed. No event, no enqueue (ADR-082, ADR-057). A first-write race on the same id is a 409
/// (unique primary key), which the caller's next push supersedes.
/// </summary>
public sealed class RecordProductSalesStatsCommandHandler(
    ILogger<RecordProductSalesStatsCommandHandler> logger,
    IAggregateRootRepository<ProductAggregate> productRepository,
    IAggregateRootRepository<ProductSalesStatsAggregate> statsRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordProductSalesStatsCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordProductSalesStatsCommand command)
    {
        logger.LogInformation("Recording sales stats for product {ProductId}. CorrelationId: {CorrelationId}", command.ProductId, command.CorrelationId);

        if (!await productRepository.ExistAsync(p => p.Id == command.ProductId))
            return Violation(BusinessViolationCodes.ProductNotFound, nameof(command.ProductId), $"Product '{command.ProductId}' was not found.");

        var stats = await statsRepository.GetByIdAsync(command.ProductId);
        var isNew = stats is null;
        stats ??= new ProductSalesStatsAggregate(command.ProductId);

        bool changed;
        try
        {
            changed = stats.Apply(new ProductSalesStatsApplyDto(command.UnitsSold90d, command.AsOf, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (!changed) return new CommandResponse { IsSuccess = true };

        if (isNew)
            await statsRepository.SaveAsync(stats, EventPublishOption.DoNotPublish);
        else
            await statsRepository.UpdateAsync(stats, EventPublishOption.DoNotPublish);

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: nameof(ProductSalesStatsAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}

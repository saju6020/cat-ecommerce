using CatEcommerce.Application.Commands.Product;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordProductSalesStatsCommandValidator : AbstractValidator<RecordProductSalesStatsCommand>
{
    public RecordProductSalesStatsCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.UnitsSold90d).GreaterThanOrEqualTo(0);
    }
}

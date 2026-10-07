using CatEcommerce.Application.Commands.Product;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-SHP-001 section 11.2 -- "Amount >= 0.01 with <= 2 dp"; "EffectiveFrom has an
/// offset" is guaranteed by <c>DateTimeOffset</c> itself. Positivity and back-dating are re-checked
/// by the aggregate (defence in depth, section 8.5).</summary>
public sealed class ScheduleSkuPriceCommandValidator : AbstractValidator<ScheduleSkuPriceCommand>
{
    public const decimal MaxAmount = 9_999_999_999.99m;

    public ScheduleSkuPriceCommandValidator()
    {
        RuleFor(x => x.SkuPriceId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0.01m);
        // Row 29: product.sku_price.amount is numeric(12,2) -- ten integer digits. Above that Postgres raises
        // a numeric overflow, which would otherwise surface as a 500.
        RuleFor(x => x.Amount).LessThanOrEqualTo(MaxAmount).WithMessage($"Amount cannot exceed {MaxAmount:0.00}.");
        RuleFor(x => x.Amount).Must(a => decimal.Round(a, 2) == a).WithMessage("Amount cannot have more than two decimal places.");
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
    }
}

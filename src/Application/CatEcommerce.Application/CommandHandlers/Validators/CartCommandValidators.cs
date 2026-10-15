using System.Text.RegularExpressions;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

internal static partial class CartValidationRules
{
    /// <summary>A guest-cart token hash is exactly 64 lowercase hex characters (SHA-256).</summary>
    public static bool IsTokenHash(string? value) => value is not null && TokenHashPattern().IsMatch(value);

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex TokenHashPattern();
}

public sealed class AddCartLineCommandValidator : AbstractValidator<AddCartLineCommand>
{
    public AddCartLineCommandValidator()
    {
        RuleFor(x => x.NewCartId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.Quantity).InclusiveBetween(1, OrderRules.MaxQuantityPerLineHardCap);
        RuleFor(x => x.CatId).Must(id => id is null || id != Guid.Empty).WithMessage("must not be empty.");
        RuleFor(x => x.GuestTokenHash).Must(h => h is null || CartValidationRules.IsTokenHash(h)).WithMessage("is not a valid cart token hash.");
        RuleFor(x => x.NewGuestTokenHash).Must(h => h is null || CartValidationRules.IsTokenHash(h)).WithMessage("is not a valid cart token hash.");
    }
}

public sealed class UpdateCartLineCommandValidator : AbstractValidator<UpdateCartLineCommand>
{
    public UpdateCartLineCommandValidator()
    {
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.Quantity).Must(q => q is null || (q >= 1 && q <= OrderRules.MaxQuantityPerLineHardCap)).WithMessage("must be 1 to 999.");
        RuleFor(x => x.CatId).Must(id => id is null || id != Guid.Empty).WithMessage("must not be empty.");
        RuleFor(x => x.GuestTokenHash).Must(h => h is null || CartValidationRules.IsTokenHash(h)).WithMessage("is not a valid cart token hash.");
    }
}

public sealed class RemoveCartLineCommandValidator : AbstractValidator<RemoveCartLineCommand>
{
    public RemoveCartLineCommandValidator()
    {
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.GuestTokenHash).Must(h => h is null || CartValidationRules.IsTokenHash(h)).WithMessage("is not a valid cart token hash.");
    }
}

public sealed class MergeGuestCartCommandValidator : AbstractValidator<MergeGuestCartCommand>
{
    public MergeGuestCartCommandValidator()
    {
        RuleFor(x => x.NewCartId).NotEmpty();
        RuleFor(x => x.GuestTokenHash).Must(CartValidationRules.IsTokenHash).WithMessage("is not a valid cart token hash.");
    }
}

public sealed class PurgeStaleGuestCartsCommandValidator : AbstractValidator<PurgeStaleGuestCartsCommand>;

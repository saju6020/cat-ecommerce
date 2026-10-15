using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-ORD-001 §11.2 -- shape only. Availability, the zone, the payment option, the fingerprint's <i>meaning</i> and the policies are the handler's.
/// The fingerprint is a SHA-256 in lower-case hex; the discount code and recipient fields have their bounds; counter and in-store values are not
/// accepted on the online path.</summary>
public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.FulfilmentMethod).Must(m => m is not null && (m == FulfilmentMethod.Delivery || m == FulfilmentMethod.Pickup)).WithMessage("must be Delivery or Pickup.");
        RuleFor(x => x.PaymentMethod).Must(m => m is not null && m != OrderPaymentMethod.InStore).WithMessage("must be CashOnDelivery, BankTransfer or PayOnCollection.");
        RuleFor(x => x.DeliveryAddressId).Must(id => id is null || id != Guid.Empty).WithMessage("must not be empty.");
        RuleFor(x => x.PickupBranchId).Must(id => id is null || id != Guid.Empty).WithMessage("must not be empty.");
        RuleFor(x => x.DiscountCode).MaximumLength(40).NoControlCharacters();
        RuleFor(x => x.RecipientName).MaximumLength(OrderRules.MaxRecipientNameLength).NoControlCharacters();
        RuleFor(x => x.RecipientPhone).MaximumLength(OrderRules.MaxRecipientPhoneLength).NoControlCharacters();
        RuleFor(x => x.QuoteFingerprint).Must(f => f is { Length: 64 } && f.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')).WithMessage("is not a valid quote fingerprint.");
    }
}

using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Aggregates.Order;

/// <summary>HO-ORD-001 C-5 -- a free-text field of a money record (a transfer reference, a terminal slip reference, a refund reference) that looks like a
/// card number is refused: it would be stored in a table nothing is ever deleted from and copied into the audit trail. The refused value is never echoed,
/// logged or stored. Reuses Billing's one <see cref="CardNumberGuard"/> and its violation code and message.</summary>
public static class OrderFreeText
{
    public static void RefuseCardNumber(string? text, string property)
    {
        if (CardNumberGuard.LooksLikeCardNumber(text))
            throw new DomainRuleViolationException(BusinessViolationCodes.ReferenceLooksLikeCardNumber, BillingMessages.ReferenceLooksLikeCardNumber, property);
    }
}

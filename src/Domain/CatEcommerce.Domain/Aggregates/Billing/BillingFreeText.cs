using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>HO-BIL-001 section 8.8.2 (C-5): every free-text field of a money record is refused when it looks like a card number -- it would be stored
/// in a Forbidden-to-delete table and copied into the audit trail. The refused value is never echoed, logged or stored.</summary>
internal static class BillingFreeText
{
    public static void RefuseCardNumber(string? text, string property)
    {
        if (CardNumberGuard.LooksLikeCardNumber(text))
            throw new DomainRuleViolationException(BusinessViolationCodes.ReferenceLooksLikeCardNumber, BillingMessages.ReferenceLooksLikeCardNumber, property);
    }
}

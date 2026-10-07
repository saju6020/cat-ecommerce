using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-ACC-001 §10.2 / ADR-038. <c>Incomplete ⇔ PasswordCredential is null</c> -- one function,
/// called by the aggregate and by every read-side query, so neither can disagree.
/// </summary>
public static class CustomerRecordStatusRules
{
    public static CustomerRecordStatus Derive(bool hasPassword) =>
        hasPassword ? CustomerRecordStatus.Complete : CustomerRecordStatus.Incomplete;
}

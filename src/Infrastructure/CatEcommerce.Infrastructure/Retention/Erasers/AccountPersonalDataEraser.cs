using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Retention.Erasers;

/// <summary>
/// HO-XCT-001 section 8.8 -- the Account context eraser (<c>ContextKey = "account"</c>). The account row itself is anonymised in place by
/// <see cref="CustomerAccountAggregate.Anonymise"/> (HO-ACC-001 ADR-041); what this eraser guarantees is the part that happens AFTER that commit by
/// event handlers which can fail: the address book and the marketing consents are cleared, through their own <c>Clear</c> methods and repositories
/// (contract rule 3). Nothing in this context is inside a retained record, so the answer is never <c>Held</c>.
///
/// Idempotent: an address book without addresses or a preferences row without consents is left untouched (no write). A customer with no account
/// row at all has nothing to erase (<c>Completed</c>); an account that is NOT anonymised is a recoverable problem, <c>Failed("AccountNotAnonymised")</c>,
/// which never happens in the normal flow because the ledger is only opened by the anonymisation event.
/// </summary>
public sealed class AccountPersonalDataEraser(
    IAggregateRootRepository<CustomerAccountAggregate> customers,
    IAggregateRootRepository<CustomerAddressBookAggregate> addressBooks,
    IAggregateRootRepository<CustomerNotificationPreferencesAggregate> preferences)
    : IPersonalDataEraser
{
    public const string AccountNotAnonymised = "AccountNotAnonymised";

    public string ContextKey => "account";

    public async Task<ErasureOutcome> EraseAsync(Guid customerAccountId, DateTimeOffset asOf, UserContext actor, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(customerAccountId);
        if (customer is null) return new ErasureOutcome.Completed();
        if (customer.Status != CustomerAccountStatus.Anonymised) return new ErasureOutcome.Failed(AccountNotAnonymised);

        var correlationId = Guid.NewGuid();

        var addressBook = await addressBooks.GetByIdAsync(customerAccountId);
        if (addressBook is not null && (addressBook.Addresses.Count > 0 || addressBook.DefaultAddressId is not null))
        {
            addressBook.Clear(new CustomerAddressBookClearDto(actor, correlationId));
            await addressBooks.UpdateAsync(addressBook, EventPublishOption.InMemoryPublishOnly);
        }

        var notificationPreferences = await preferences.GetByIdAsync(customerAccountId);
        if (notificationPreferences is not null && notificationPreferences.MarketingConsents.Count > 0)
        {
            notificationPreferences.Clear(new CustomerNotificationPreferencesClearDto(actor, correlationId));
            await preferences.UpdateAsync(notificationPreferences, EventPublishOption.InMemoryPublishOnly);
        }

        return new ErasureOutcome.Completed();
    }
}

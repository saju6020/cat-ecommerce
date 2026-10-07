using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-NTF-001 §8.6 -- one attempt to deliver a <c>NotificationAggregate</c> on its
/// channel. <see cref="ProviderResponse"/> is truncated to 1000 characters and never the request
/// (§8.9 rule 6).</summary>
public sealed class DeliveryAttempt
{
    public Guid Id { get; private set; }
    public DateTimeOffset AttemptedOn { get; private set; }
    public SendOutcomeKind Outcome { get; private set; } = default!;
    public string ProviderName { get; private set; } = default!;
    public string? ProviderMessageId { get; private set; }
    public string? ProviderResponse { get; private set; }

    private DeliveryAttempt() { }

    public DeliveryAttempt(Guid id, DateTimeOffset attemptedOn, SendOutcomeKind outcome, string providerName, string? providerMessageId, string? providerResponse)
    {
        Id = id;
        AttemptedOn = attemptedOn;
        Outcome = outcome;
        ProviderName = providerName;
        ProviderMessageId = providerMessageId?.Length > 200 ? providerMessageId[..200] : providerMessageId;
        ProviderResponse = providerResponse?.Length > 1000 ? providerResponse[..1000] : providerResponse;
    }
}

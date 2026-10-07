using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Account;

/// <summary>
/// HO-ACC-001 §8.6 / ADR-035. Id is the customer id. Only marketing cells are ever stored --
/// there is no representation of "transactional: off" (FR-NTF-4 by construction). Extended by
/// HO-NTF-001 §8.5 with <c>PreferredChannel</c> (Amended row, §1) -- not built by this pass;
/// see the remaining-work note in the flow doc.
/// </summary>
public sealed class CustomerNotificationPreferencesAggregate : AggregateRoot
{
    private readonly List<MarketingConsent> _marketingConsents = [];

    public IReadOnlyList<MarketingConsent> MarketingConsents => _marketingConsents;

    /// <summary>HO-NTF-001 §8.5 -- only <see cref="NotificationChannel.Sms"/> or
    /// <see cref="NotificationChannel.Email"/>; <c>null</c> until chosen. The effective preferred
    /// channel is <c>PreferredChannel ?? (mobile present ? Sms : Email)</c>, computed by
    /// <c>INotificationRecipientDirectory</c>, not here (this aggregate does not know the account's
    /// identifiers).</summary>
    public NotificationChannel? PreferredChannel { get; private set; }

    private CustomerNotificationPreferencesAggregate() { }

    public CustomerNotificationPreferencesAggregate(Guid id) : base(id) { }

    public void Open(CustomerNotificationPreferencesOpenDto dto)
    {
        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new CustomerNotificationPreferencesOpenedEvent
        {
            CustomerId = Id,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    /// <summary>All-or-nothing: every cell in <paramref name="dto"/> must be editable, or nothing
    /// is applied. A first explicit decision is recorded even if it equals the default -- it is a
    /// consent record.</summary>
    public void ChangePreferences(ChangeNotificationPreferencesDto dto)
    {
        // Non-empty Changes is validated shape-only by the command validator (§11.2, C8).
        var seen = new HashSet<(int Channel, int Category)>();
        foreach (var change in dto.Changes)
        {
            var key = (change.Channel.Value, change.Category.Value);
            if (!seen.Add(key))
                throw new DomainRuleViolationException(
                    BusinessViolationCodes.DuplicateNotificationPreferenceChange,
                    "The same channel and category cannot be changed twice in one request.",
                    nameof(dto.Changes));

            if (!NotificationPreferenceRules.IsEditable(change.Category))
                throw new DomainRuleViolationException(
                    BusinessViolationCodes.NotificationPreferenceNotEditable,
                    "Transactional notifications cannot be turned off.",
                    nameof(dto.Changes));
        }

        var changeDescriptions = new List<string>();
        foreach (var change in dto.Changes)
        {
            var existing = _marketingConsents.FirstOrDefault(c => c.Channel == change.Channel);
            if (existing is null)
                _marketingConsents.Add(new MarketingConsent(Guid.NewGuid(), change.Channel, change.IsGranted, dto.RecordedOn, dto.Source));
            else
                existing.Update(change.IsGranted, dto.RecordedOn, dto.Source);

            changeDescriptions.Add($"{change.Channel.DisplayName}/{change.Category.DisplayName}={change.IsGranted}");
        }

        AddDomainEvent(new NotificationPreferencesChangedEvent
        {
            CustomerId = Id,
            Changes = changeDescriptions,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    /// <summary>Anonymisation only (§8.10a).</summary>
    public void Clear(CustomerNotificationPreferencesClearDto dto)
    {
        _marketingConsents.Clear();

        AddDomainEvent(new CustomerNotificationPreferencesClearedEvent
        {
            CustomerId = Id,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    /// <summary>HO-NTF-001 §8.5/§11.4 (amended C8). Only <see cref="NotificationChannel.Sms"/> or
    /// <see cref="NotificationChannel.Email"/> -- the handler checks the account actually holds
    /// that identifier (<c>PreferredChannelHasNoDestination</c>), which this aggregate cannot see.</summary>
    public void ChangePreferredChannel(NotificationChannel channel, Guid correlationId, Platform.Infrastructure.Common.Security.UserContext userContext)
    {
        if (channel != NotificationChannel.Sms && channel != NotificationChannel.Email)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.InvalidPreferredChannel,
                "The preferred channel must be Sms or Email.",
                nameof(PreferredChannel));

        PreferredChannel = channel;

        AddDomainEvent(new NotificationPreferencesChangedEvent
        {
            CustomerId = Id,
            Changes = [$"PreferredChannel={channel.DisplayName}"],
            CorrelationId = correlationId,
            UserContext = userContext
        });

        UpdateVersion();
    }

    public bool Permits(NotificationChannel channel, NotificationCategory category)
    {
        var granted = _marketingConsents.FirstOrDefault(c => c.Channel == channel)?.IsGranted
            ?? NotificationPreferenceRules.DefaultMarketingGranted;

        return NotificationPreferenceRules.Permits(channel, category, granted);
    }
}

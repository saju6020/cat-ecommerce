using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Account;

/// <summary>
/// HO-ACC-001 §8.5 / ADR-034. Append-only: created once, never updated. Single use by security
/// stamp rotation, not by a stored flag -- there is no <c>Status</c>/<c>UsedOn</c>, which would go
/// stale (ADR-023's reasoning).
/// </summary>
public sealed class PasswordResetTokenAggregate : AggregateRoot
{
    public AccountKind AccountKind { get; private set; } = default!;
    public Guid AccountId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTimeOffset IssuedOn { get; private set; }
    public DateTimeOffset ExpiresOn { get; private set; }
    public Guid IssuedForSecurityStamp { get; private set; }
    public PasswordResetChannel Channel { get; private set; } = default!;

    private PasswordResetTokenAggregate() { }

    public PasswordResetTokenAggregate(Guid id) : base(id) { }

    public void Issue(PasswordResetTokenIssueDto dto)
    {
        AccountKind = dto.AccountKind;
        AccountId = dto.AccountId;
        TokenHash = dto.TokenHash;
        IssuedOn = dto.IssuedOn;
        ExpiresOn = dto.ExpiresOn;
        IssuedForSecurityStamp = dto.IssuedForSecurityStamp;
        Channel = dto.Channel;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new PasswordResetRequestedEvent
        {
            AccountKind = AccountKind,
            AccountId = AccountId,
            Channel = Channel,
            ExpiresOn = ExpiresOn,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    /// <summary>Non-mutating guard. Both failures collapse to
    /// <see cref="BusinessViolationCodes.InvalidPasswordResetToken"/> in the handler.</summary>
    public void EnsureUsable(DateTimeOffset asOf, Guid currentStamp)
    {
        if (asOf >= ExpiresOn)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.PasswordResetTokenExpired, "This reset token has expired.", nameof(ExpiresOn));

        if (IssuedForSecurityStamp != currentStamp)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.PasswordResetTokenSuperseded, "This reset token is no longer valid.", nameof(IssuedForSecurityStamp));
    }
}

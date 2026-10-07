using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Account;

/// <summary>HO-ACC-001 §8.7. Roles are the aggregate's own invariant (FR-ACC-6).</summary>
public sealed class StaffAccountAggregate : AggregateRoot
{
    private readonly List<string> _roleIds = [];

    public string FullName { get; private set; } = default!;
    public MobileNumber? MobileNumber { get; private set; }
    public EmailAddress? EmailAddress { get; private set; }
    public PasswordCredential? PasswordCredential { get; private set; }
    public ValueObjects.LoginThrottle LoginThrottle { get; private set; } = ValueObjects.LoginThrottle.None;
    public Guid SecurityStamp { get; private set; }
    public DateTimeOffset? LastSuccessfulLoginOn { get; private set; }
    public IReadOnlyList<string> RoleIds => _roleIds;

    private StaffAccountAggregate() { }

    public StaffAccountAggregate(Guid id) : base(id) { }

    public void Create(StaffAccountCreationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || dto.FullName.Length > 200)
            throw Violation(BusinessViolationCodes.NameRequired, "A name is required.", nameof(FullName));

        if (dto.MobileNumber is null && dto.EmailAddress is null)
            throw Violation(BusinessViolationCodes.IdentifierRequired, "At least one of a mobile number or email address is required.", nameof(MobileNumber));

        var normalisedRoles = NormaliseRoles(dto.RoleIds);

        FullName = dto.FullName;
        MobileNumber = dto.MobileNumber;
        EmailAddress = dto.EmailAddress;
        PasswordCredential = null;
        LoginThrottle = ValueObjects.LoginThrottle.None;
        SecurityStamp = Guid.NewGuid();
        _roleIds.Clear();
        _roleIds.AddRange(normalisedRoles);

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new StaffAccountCreatedEvent
        {
            StaffAccountId = Id,
            RoleIds = _roleIds.ToList(),
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void ChangeRoles(StaffRoleChangeDto dto)
    {
        var normalisedRoles = NormaliseRoles(dto.RoleIds);

        if (_roleIds.SequenceEqual(normalisedRoles)) return;

        var previous = _roleIds.ToList();
        _roleIds.Clear();
        _roleIds.AddRange(normalisedRoles);
        SecurityStamp = Guid.NewGuid();

        AddDomainEvent(new StaffAccountRolesChangedEvent
        {
            StaffAccountId = Id,
            PreviousRoleIds = previous,
            NewRoleIds = _roleIds.ToList(),
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void RecordSuccessfulLogin(LoginSuccessDto dto)
    {
        EnsureCanSignIn(dto.AsOf);

        LoginThrottle = ValueObjects.LoginThrottle.None;
        LastSuccessfulLoginOn = dto.AsOf;

        AddDomainEvent(new StaffSignedInEvent
        {
            AccountId = Id,
            OccurredOn = dto.AsOf,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void RecordFailedLogin(LoginFailureDto dto)
    {
        LoginThrottle = LoginThrottle.AfterFailure(dto.AsOf);

        AddDomainEvent(new StaffSignInFailedEvent
        {
            AccountId = Id,
            Outcome = SignInOutcome.InvalidCredentials,
            ConsecutiveFailures = LoginThrottle.FailedCount,
            LockedUntil = LoginThrottle.LockedUntil,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void SetPassword(PasswordSetDto dto)
    {
        var wasFirstPassword = PasswordCredential is null;

        PasswordCredential = dto.Credential;
        SecurityStamp = Guid.NewGuid();
        LoginThrottle = ValueObjects.LoginThrottle.None;

        AddDomainEvent(new StaffPasswordSetEvent
        {
            StaffAccountId = Id,
            WasFirstPassword = wasFirstPassword,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    /// <summary>No status check for staff -- public for the same reason as the customer aggregate's
    /// equivalent guard (a non-mutating probe the sign-in handler calls before verifying).</summary>
    public void EnsureCanSignIn(DateTimeOffset asOf)
    {
        if (PasswordCredential is null)
            throw Violation(BusinessViolationCodes.InvalidCredentials, "No password has been set for this account.", nameof(PasswordCredential));

        if (LoginThrottle.IsLocked(asOf))
            throw Violation(BusinessViolationCodes.AccountLocked, "This account is temporarily locked.", nameof(LoginThrottle));
    }

    private static List<string> NormaliseRoles(IReadOnlyList<string> roleIds)
    {
        var trimmed = roleIds.Select(r => r.Trim()).Where(r => r.Length > 0).Distinct(StringComparer.Ordinal).ToList();

        if (trimmed.Count == 0)
            throw Violation(BusinessViolationCodes.StaffRolesRequired, "At least one staff role is required.", nameof(RoleIds));

        foreach (var role in trimmed)
            if (!StaffRoles.All.Contains(role, StringComparer.Ordinal))
                throw Violation(BusinessViolationCodes.UnknownStaffRole, $"'{role}' is not a known staff role.", nameof(RoleIds));

        return StaffRoles.All.Where(trimmed.Contains).ToList();
    }

    private static DomainRuleViolationException Violation(string code, string message, string property) =>
        new(code, message, property);
}

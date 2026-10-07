using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Account;

/// <summary>
/// HO-ACC-001 §8.3. The customer half of the Account context -- a separate root from
/// <see cref="StaffAccountAggregate"/> (ADR-024): different creators, different contents,
/// different readers, and FR-ACC-3's structural rule that a customer credential never grants a
/// staff session.
/// </summary>
public sealed class CustomerAccountAggregate : AggregateRoot
{
    private readonly List<string> _additionalPhoneNumbers = [];

    public string FullName { get; private set; } = default!;
    public MobileNumber? MobileNumber { get; private set; }
    public EmailAddress? EmailAddress { get; private set; }
    public PasswordCredential? PasswordCredential { get; private set; }
    public LoginThrottle LoginThrottle { get; private set; } = ValueObjects.LoginThrottle.None;
    public Guid SecurityStamp { get; private set; }
    public RegistrationChannel RegistrationChannel { get; private set; } = default!;
    public CustomerAccountStatus Status { get; private set; } = default!;
    public IReadOnlyList<string> AdditionalPhoneNumbers => _additionalPhoneNumbers;
    public DateTimeOffset? LastSuccessfulLoginOn { get; private set; }
    public Guid? MergedIntoCustomerId { get; private set; }
    public DateTimeOffset? MergedOn { get; private set; }
    public DateTimeOffset? AnonymisedOn { get; private set; }
    public DateTimeOffset? LastPersonalDataExportOn { get; private set; }

    /// <summary>Computed only (ADR-038) -- never mapped/persisted.</summary>
    public CustomerRecordStatus RecordStatus => CustomerRecordStatusRules.Derive(PasswordCredential is not null);

    private CustomerAccountAggregate() { }

    public CustomerAccountAggregate(Guid id) : base(id) { }

    public void Register(CustomerRegistrationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || dto.FullName.Length > 200)
            throw Violation(BusinessViolationCodes.NameRequired, "A name is required.", nameof(FullName));

        FullName = dto.FullName;
        MobileNumber = dto.MobileNumber;
        EmailAddress = dto.EmailAddress;
        PasswordCredential = dto.Credential;
        RegistrationChannel = RegistrationChannel.SelfService;
        Status = CustomerAccountStatus.Active;
        LoginThrottle = ValueObjects.LoginThrottle.None;
        SecurityStamp = Guid.NewGuid();

        EnsureHasIdentifier();

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new CustomerRegisteredEvent
        {
            CustomerId = Id,
            HasMobile = MobileNumber is not null,
            HasEmail = EmailAddress is not null,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void RegisterWalkIn(WalkInCustomerRegistrationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || dto.FullName.Length > 200)
            throw Violation(BusinessViolationCodes.NameRequired, "A name is required.", nameof(FullName));

        FullName = dto.FullName;
        MobileNumber = dto.MobileNumber;
        EmailAddress = null;
        PasswordCredential = null;
        RegistrationChannel = RegistrationChannel.Reception;
        Status = CustomerAccountStatus.Active;
        LoginThrottle = ValueObjects.LoginThrottle.None;
        SecurityStamp = Guid.NewGuid();

        EnsureHasIdentifier();

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new WalkInCustomerRegisteredEvent
        {
            CustomerId = Id,
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

        AddDomainEvent(new CustomerSignedInEvent
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
        // A merged or anonymised account records nothing and stays dead.
        if (Status != CustomerAccountStatus.Active) return;

        LoginThrottle = LoginThrottle.AfterFailure(dto.AsOf);

        AddDomainEvent(new CustomerSignInFailedEvent
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
        if (Status != CustomerAccountStatus.Active)
            throw Violation(BusinessViolationCodes.AccountNotActive, "This account is not active.", nameof(Status));

        var wasFirstPassword = PasswordCredential is null;

        PasswordCredential = dto.Credential;
        SecurityStamp = Guid.NewGuid();
        LoginThrottle = ValueObjects.LoginThrottle.None;

        AddDomainEvent(new CustomerPasswordSetEvent
        {
            CustomerId = Id,
            WasFirstPassword = wasFirstPassword,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void UpdateContactDetails(CustomerContactDetailsDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || dto.FullName.Length > 200)
            throw Violation(BusinessViolationCodes.NameRequired, "A name is required.", nameof(FullName));

        if (dto.AdditionalPhoneNumbers.Count > CustomerProfileRules.MaxAdditionalPhoneNumbers)
            throw Violation(
                BusinessViolationCodes.AdditionalPhoneNumberLimitExceeded,
                $"No more than {CustomerProfileRules.MaxAdditionalPhoneNumbers} additional phone numbers are allowed.",
                nameof(dto.AdditionalPhoneNumbers));

        var normalisedAdditional = dto.AdditionalPhoneNumbers.Select(n => n.Value).ToList();
        if (normalisedAdditional.Distinct().Count() != normalisedAdditional.Count ||
            normalisedAdditional.Any(n => n == MobileNumber?.Value))
            throw Violation(
                BusinessViolationCodes.DuplicatePhoneNumber,
                "An additional phone number must not repeat the primary mobile number or another additional number.",
                nameof(dto.AdditionalPhoneNumbers));

        var emailChanging = !Equals(EmailAddress, dto.EmailAddress);
        if (emailChanging && MobileNumber is null)
            throw Violation(
                BusinessViolationCodes.PrimaryIdentifierReadOnly,
                "An email-only account's email is its only login identifier and cannot be changed here.",
                nameof(dto.EmailAddress));

        var nameChanging = FullName != dto.FullName;
        var additionalChanging = !_additionalPhoneNumbers.SequenceEqual(normalisedAdditional);

        if (!nameChanging && !emailChanging && !additionalChanging) return;

        FullName = dto.FullName;
        if (emailChanging) EmailAddress = dto.EmailAddress;
        _additionalPhoneNumbers.Clear();
        _additionalPhoneNumbers.AddRange(normalisedAdditional);

        EnsureHasIdentifier();

        AddDomainEvent(new CustomerContactDetailsUpdatedEvent
        {
            CustomerId = Id,
            NameChanged = nameChanging,
            EmailChanged = emailChanging,
            AdditionalPhoneNumbersChanged = additionalChanging,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void MergeInto(CustomerMergeDto dto)
    {
        if (dto.SurvivingCustomerId == Id)
            throw Violation(
                BusinessViolationCodes.CannotMergeCustomerIntoItself,
                "A customer record cannot be merged into itself.",
                nameof(dto.SurvivingCustomerId));

        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 1000)
            throw Violation(BusinessViolationCodes.ReasonRequired, "A reason is required to merge customer records.", nameof(dto.Reason));

        if (Status == CustomerAccountStatus.Anonymised)
            throw Violation(BusinessViolationCodes.AccountNotActive, "This account is not active.", nameof(Status));

        if (MergedIntoCustomerId is not null && MergedIntoCustomerId != dto.SurvivingCustomerId)
            throw Violation(
                BusinessViolationCodes.CustomerAlreadyMerged,
                "This customer record has already been merged into a different record.",
                nameof(MergedIntoCustomerId));

        if (MergedIntoCustomerId == dto.SurvivingCustomerId)
        {
            AddDomainEvent(new CustomerMergedEvent
            {
                DuplicateCustomerId = Id,
                SurvivingCustomerId = dto.SurvivingCustomerId,
                Reason = dto.Reason,
                IsReapplication = true,
                CorrelationId = dto.CorrelationId,
                UserContext = dto.UserContext
            });
            UpdateVersion();
            return;
        }

        var sharesAdditionalOrPrimaryMobile =
            (MobileNumber is not null && dto.SurvivorMobile is not null && MobileNumber.Value == dto.SurvivorMobile.Value) ||
            (MobileNumber is not null && dto.SurvivorAdditionalPhoneNumbers.Contains(MobileNumber.Value)) ||
            (dto.SurvivorMobile is not null && _additionalPhoneNumbers.Contains(dto.SurvivorMobile.Value)) ||
            _additionalPhoneNumbers.Intersect(dto.SurvivorAdditionalPhoneNumbers).Any();

        var sharesEmail = EmailAddress is not null && dto.SurvivorEmail is not null && EmailAddress.Value == dto.SurvivorEmail.Value;

        if (!sharesAdditionalOrPrimaryMobile && !sharesEmail)
            throw Violation(
                BusinessViolationCodes.CustomersNotDuplicates,
                "These customer records do not share a contact number or email address.",
                nameof(dto.SurvivingCustomerId));

        MergedIntoCustomerId = dto.SurvivingCustomerId;
        MergedOn = dto.MergedOn;
        Status = CustomerAccountStatus.Merged;
        SecurityStamp = Guid.NewGuid();

        AddDomainEvent(new CustomerMergedEvent
        {
            DuplicateCustomerId = Id,
            SurvivingCustomerId = dto.SurvivingCustomerId,
            Reason = dto.Reason,
            IsReapplication = false,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void Anonymise(CustomerAnonymisationDto dto)
    {
        if (Status == CustomerAccountStatus.Anonymised)
            throw Violation(BusinessViolationCodes.AccountAlreadyAnonymised, "This account has already been anonymised.", nameof(Status));

        if (Status != CustomerAccountStatus.Active)
            throw Violation(BusinessViolationCodes.AccountNotActive, "This account is not active.", nameof(Status));

        FullName = AnonymisationRules.PlaceholderFullName;
        MobileNumber = null;
        EmailAddress = null;
        PasswordCredential = null;
        LastSuccessfulLoginOn = null;
        _additionalPhoneNumbers.Clear();
        LoginThrottle = ValueObjects.LoginThrottle.None;
        SecurityStamp = Guid.NewGuid();
        Status = CustomerAccountStatus.Anonymised;
        AnonymisedOn = dto.AnonymisedOn;

        AddDomainEvent(new CustomerAccountAnonymisedEvent
        {
            CustomerId = Id,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void RecordPersonalDataExport(PersonalDataExportDto dto)
    {
        if (Status != CustomerAccountStatus.Active)
            throw Violation(BusinessViolationCodes.AccountNotActive, "This account is not active.", nameof(Status));

        LastPersonalDataExportOn = dto.ExportedOn;

        AddDomainEvent(new CustomerPersonalDataExportedEvent
        {
            CustomerId = Id,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    /// <summary>In order: not active, no credential, locked -- all three collapse to the same
    /// public response at the Api (§12.4 Rule 1, R1). Public (not the private guard §8.3 describes
    /// verbatim) because the sign-in handler must be able to check "would this refuse?" before
    /// calling <c>IPasswordHasher.Verify</c> -- verifying a password against a locked or
    /// credential-less account would still burn hasher work and blur the account-state signal the
    /// lockout is meant to hide. Non-mutating; safe to call as a probe.</summary>
    public void EnsureCanSignIn(DateTimeOffset asOf)
    {
        if (Status != CustomerAccountStatus.Active)
            throw Violation(BusinessViolationCodes.AccountNotActive, "This account is not active.", nameof(Status));

        if (PasswordCredential is null)
            throw Violation(BusinessViolationCodes.InvalidCredentials, "No password has been set for this account.", nameof(PasswordCredential));

        if (LoginThrottle.IsLocked(asOf))
            throw Violation(BusinessViolationCodes.AccountLocked, "This account is temporarily locked.", nameof(LoginThrottle));
    }

    private void EnsureHasIdentifier()
    {
        if (Status == CustomerAccountStatus.Anonymised) return;

        if (MobileNumber is null && EmailAddress is null)
            throw Violation(BusinessViolationCodes.IdentifierRequired, "At least one of a mobile number or email address is required.", nameof(MobileNumber));
    }

    private static DomainRuleViolationException Violation(string code, string message, string property) =>
        new(code, message, property);
}

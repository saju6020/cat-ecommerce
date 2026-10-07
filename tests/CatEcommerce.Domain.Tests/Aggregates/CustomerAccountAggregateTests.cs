using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates;

/// <summary>HO-ACC-001 §15 -- Registration (B), Sign-in (C), Reset (D), Profile (E), Merge (I), Deletion (J).</summary>
public sealed class CustomerAccountAggregateTests
{
    private static PasswordCredential Credential() => PasswordCredential.Create("hash", "argon2id", DateTimeOffset.UtcNow);

    private static CustomerAccountAggregate RegisteredCustomer(MobileNumber? mobile = null, EmailAddress? email = null)
    {
        // Only default a mobile number when neither identifier was explicitly supplied --
        // otherwise callers testing an email-only account (mobile: null) would always get one
        // anyway, defeating the point of the test.
        var effectiveMobile = mobile ?? (email is null ? MobileNumber.Create("01712345678") : null);

        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.Register(new CustomerRegistrationDto(
            "Jane Doe", effectiveMobile, email, Credential(),
            TestUserContext.Create(), Guid.NewGuid()));
        return customer;
    }

    // U-B1
    [Fact]
    public void Register_Valid_SetsPropertiesRaisesOneEventAndBumpsVersion()
    {
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        var versionBefore = customer.Version;

        customer.Register(new CustomerRegistrationDto(
            "Jane Doe", MobileNumber.Create("01712345678"), null, Credential(), TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(RegistrationChannel.SelfService, customer.RegistrationChannel);
        Assert.Equal(CustomerAccountStatus.Active, customer.Status);
        Assert.NotEqual(Guid.Empty, customer.SecurityStamp);
        Assert.Single(customer.Events);
        Assert.True(customer.Version > versionBefore);
    }

    // U-B2
    [Fact]
    public void Register_WithNeitherIdentifier_Throws()
    {
        var customer = new CustomerAccountAggregate(Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => customer.Register(new CustomerRegistrationDto(
            "Jane Doe", null, null, Credential(), TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal("IdentifierRequired", ex.ErrorCode);
    }

    // U-C1
    [Fact]
    public void RecordFailedLogin_ThreeTimes_LocksAccount()
    {
        var customer = RegisteredCustomer();
        var asOf = DateTimeOffset.UtcNow;

        customer.RecordFailedLogin(new LoginFailureDto(asOf, TestUserContext.Create(), Guid.NewGuid()));
        customer.RecordFailedLogin(new LoginFailureDto(asOf, TestUserContext.Create(), Guid.NewGuid()));
        customer.RecordFailedLogin(new LoginFailureDto(asOf, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(3, customer.LoginThrottle.FailedCount);
        Assert.True(customer.LoginThrottle.IsLocked(asOf.AddSeconds(1)));
    }

    // U-C2
    [Fact]
    public void EnsureCanSignIn_WhileLocked_Throws()
    {
        var customer = RegisteredCustomer();
        var asOf = DateTimeOffset.UtcNow;
        customer.RecordFailedLogin(new LoginFailureDto(asOf, TestUserContext.Create(), Guid.NewGuid()));
        customer.RecordFailedLogin(new LoginFailureDto(asOf, TestUserContext.Create(), Guid.NewGuid()));
        customer.RecordFailedLogin(new LoginFailureDto(asOf, TestUserContext.Create(), Guid.NewGuid()));

        var ex = Assert.Throws<DomainRuleViolationException>(() => customer.EnsureCanSignIn(asOf.AddSeconds(1)));
        Assert.Equal("AccountLocked", ex.ErrorCode);
    }

    // U-C3
    [Fact]
    public void RecordSuccessfulLogin_AfterLockExpiry_ResetsThrottle()
    {
        var customer = RegisteredCustomer();
        var asOf = DateTimeOffset.UtcNow;
        customer.RecordFailedLogin(new LoginFailureDto(asOf, TestUserContext.Create(), Guid.NewGuid()));
        customer.RecordFailedLogin(new LoginFailureDto(asOf, TestUserContext.Create(), Guid.NewGuid()));
        customer.RecordFailedLogin(new LoginFailureDto(asOf, TestUserContext.Create(), Guid.NewGuid()));

        customer.RecordSuccessfulLogin(new LoginSuccessDto(asOf.AddSeconds(10), TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(0, customer.LoginThrottle.FailedCount);
        Assert.NotNull(customer.LastSuccessfulLoginOn);
    }

    // U-C4
    [Fact]
    public void EnsureCanSignIn_NoCredential_ReturnsInvalidCredentials()
    {
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto("Walk In", MobileNumber.Create("01711111111"), TestUserContext.Create(), Guid.NewGuid()));

        var ex = Assert.Throws<DomainRuleViolationException>(() => customer.EnsureCanSignIn(DateTimeOffset.UtcNow));
        Assert.Equal("InvalidCredentials", ex.ErrorCode);
    }

    // U-D2
    [Fact]
    public void SetPassword_RotatesStampResetsThrottleAndReportsFirstPassword()
    {
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto("Walk In", MobileNumber.Create("01711111111"), TestUserContext.Create(), Guid.NewGuid()));
        var stampBefore = customer.SecurityStamp;

        customer.SetPassword(new PasswordSetDto(Credential(), TestUserContext.Create(), Guid.NewGuid()));

        Assert.NotEqual(stampBefore, customer.SecurityStamp);
        Assert.Equal(CustomerRecordStatus.Complete, customer.RecordStatus);
    }

    // U-E1
    [Fact]
    public void UpdateContactDetails_EmailOnlyAccount_ChangingEmail_Throws()
    {
        var customer = RegisteredCustomer(mobile: null, email: EmailAddress.Create("a@b.com"));

        var ex = Assert.Throws<DomainRuleViolationException>(() => customer.UpdateContactDetails(new CustomerContactDetailsDto(
            "Jane Doe", EmailAddress.Create("c@d.com"), [], TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal("PrimaryIdentifierReadOnly", ex.ErrorCode);
    }

    // U-E2
    [Fact]
    public void UpdateContactDetails_TooManyAdditionalNumbers_Throws()
    {
        var customer = RegisteredCustomer();

        var ex = Assert.Throws<DomainRuleViolationException>(() => customer.UpdateContactDetails(new CustomerContactDetailsDto(
            "Jane Doe", null,
            [MobileNumber.Create("01711111111"), MobileNumber.Create("01711111112"), MobileNumber.Create("01711111113"), MobileNumber.Create("01711111114")],
            TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal("AdditionalPhoneNumberLimitExceeded", ex.ErrorCode);
    }

    [Fact]
    public void UpdateContactDetails_DuplicateOfPrimary_Throws()
    {
        var customer = RegisteredCustomer(mobile: MobileNumber.Create("01712345678"));

        var ex = Assert.Throws<DomainRuleViolationException>(() => customer.UpdateContactDetails(new CustomerContactDetailsDto(
            "Jane Doe", null, [MobileNumber.Create("01712345678")], TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal("DuplicatePhoneNumber", ex.ErrorCode);
    }

    // U-E3
    [Fact]
    public void UpdateContactDetails_NoChange_RaisesNoEventAndDoesNotBumpVersion()
    {
        var customer = RegisteredCustomer();
        customer.ClearEvents();
        var versionBefore = customer.Version;

        customer.UpdateContactDetails(new CustomerContactDetailsDto("Jane Doe", null, [], TestUserContext.Create(), Guid.NewGuid()));

        Assert.Empty(customer.Events);
        Assert.Equal(versionBefore, customer.Version);
    }

    // U-I1
    [Fact]
    public void MergeInto_Self_Throws()
    {
        var customer = RegisteredCustomer();

        var ex = Assert.Throws<DomainRuleViolationException>(() => customer.MergeInto(new CustomerMergeDto(
            customer.Id, "duplicate", null, [], null, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal("CannotMergeCustomerIntoItself", ex.ErrorCode);
    }

    [Fact]
    public void MergeInto_BlankReason_Throws()
    {
        var customer = RegisteredCustomer();

        var ex = Assert.Throws<DomainRuleViolationException>(() => customer.MergeInto(new CustomerMergeDto(
            Guid.NewGuid(), "  ", null, [], null, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal("ReasonRequired", ex.ErrorCode);
    }

    // U-I2
    [Fact]
    public void MergeInto_SharesAdditionalNumberWithSurvivor_Succeeds()
    {
        var customer = RegisteredCustomer();
        customer.UpdateContactDetails(new CustomerContactDetailsDto("Jane Doe", null, [MobileNumber.Create("01799999999")], TestUserContext.Create(), Guid.NewGuid()));

        customer.MergeInto(new CustomerMergeDto(
            Guid.NewGuid(), "duplicate", MobileNumber.Create("01799999999"), [], null, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(CustomerAccountStatus.Merged, customer.Status);
    }

    [Fact]
    public void MergeInto_NoSharedIdentifier_Throws()
    {
        var customer = RegisteredCustomer();

        var ex = Assert.Throws<DomainRuleViolationException>(() => customer.MergeInto(new CustomerMergeDto(
            Guid.NewGuid(), "duplicate", MobileNumber.Create("01700000000"), [], null, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal("CustomersNotDuplicates", ex.ErrorCode);
    }

    // U-I3
    [Fact]
    public void MergeInto_ReapplicationWithSameSurvivor_IsIdempotent()
    {
        var customer = RegisteredCustomer();
        var survivorId = Guid.NewGuid();
        var dto = new CustomerMergeDto(survivorId, "duplicate", customer.MobileNumber, [], null, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());

        customer.MergeInto(dto);
        var stampAfterFirst = customer.SecurityStamp;
        customer.MergeInto(dto);

        Assert.Equal(stampAfterFirst, customer.SecurityStamp);
        Assert.Equal(survivorId, customer.MergedIntoCustomerId);
    }

    [Fact]
    public void MergeInto_DifferentSurvivorAfterAlreadyMerged_Throws()
    {
        var customer = RegisteredCustomer();
        customer.MergeInto(new CustomerMergeDto(Guid.NewGuid(), "duplicate", customer.MobileNumber, [], null, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));

        var ex = Assert.Throws<DomainRuleViolationException>(() => customer.MergeInto(new CustomerMergeDto(
            Guid.NewGuid(), "duplicate again", customer.MobileNumber, [], null, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal("CustomerAlreadyMerged", ex.ErrorCode);
    }

    // CODE_REVIEW gate, MEDIUM (2026-09-25): MergedOn must be exactly the caller-supplied value,
    // not a wall-clock read inside MergeInto.
    [Fact]
    public void MergeInto_SetsMergedOnToTheSuppliedTimestamp()
    {
        var customer = RegisteredCustomer();
        var mergedOn = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

        customer.MergeInto(new CustomerMergeDto(
            Guid.NewGuid(), "duplicate", customer.MobileNumber, [], null, mergedOn, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(mergedOn, customer.MergedOn);
    }

    // U-J1 / U-J2
    [Fact]
    public void Anonymise_ClearsPersonalFieldsAndRotatesStamp()
    {
        var customer = RegisteredCustomer(email: EmailAddress.Create("jane@example.com"));
        var stampBefore = customer.SecurityStamp;

        customer.Anonymise(new CustomerAnonymisationDto(DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal("Anonymised customer", customer.FullName);
        Assert.Null(customer.MobileNumber);
        Assert.Null(customer.EmailAddress);
        Assert.Null(customer.PasswordCredential);
        Assert.Equal(CustomerAccountStatus.Anonymised, customer.Status);
        Assert.NotEqual(stampBefore, customer.SecurityStamp);
    }

    [Fact]
    public void Anonymise_Twice_Throws()
    {
        var customer = RegisteredCustomer();
        customer.Anonymise(new CustomerAnonymisationDto(DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));

        var ex = Assert.Throws<DomainRuleViolationException>(() => customer.Anonymise(new CustomerAnonymisationDto(
            DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal("AccountAlreadyAnonymised", ex.ErrorCode);
    }

    // U-H1 / U-H2
    [Fact]
    public void RegisterWalkIn_HasNoCredentialAndIsIncomplete()
    {
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto("Walk In", MobileNumber.Create("01711111111"), TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(RegistrationChannel.Reception, customer.RegistrationChannel);
        Assert.Null(customer.PasswordCredential);
        Assert.Null(customer.EmailAddress);
        Assert.Equal(CustomerRecordStatus.Incomplete, customer.RecordStatus);
    }

    [Fact]
    public void RegisterWalkIn_ThenSetPassword_BecomesComplete()
    {
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto("Walk In", MobileNumber.Create("01711111111"), TestUserContext.Create(), Guid.NewGuid()));

        customer.SetPassword(new PasswordSetDto(Credential(), TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(CustomerRecordStatus.Complete, customer.RecordStatus);
        Assert.Equal(RegistrationChannel.Reception, customer.RegistrationChannel);
    }

    // U-B3 / reflection guard -- no verification/OTP/2FA/CAPTCHA member anywhere on the aggregate.
    [Fact]
    public void Aggregate_HasNoVerificationOtpTwoFactorOrCaptchaMembers()
    {
        var forbidden = new[] { "Verif", "Otp", "TwoFactor", "Captcha" };
        var members = typeof(CustomerAccountAggregate).GetMembers()
            .Select(m => m.Name)
            .Concat(typeof(CustomerRegistrationDto).GetProperties().Select(p => p.Name));

        foreach (var member in members)
            Assert.DoesNotContain(forbidden, f => member.Contains(f, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// U-A5. Reflects over every public string property after Anonymise -- fails on purpose if a
    /// new personal field is added to the aggregate without being cleared in Anonymise (§8.10a).
    /// </summary>
    [Fact]
    public void Anonymise_EveryPublicStringProperty_IsNullPlaceholderOrAllowListed()
    {
        var allowList = new HashSet<string> { nameof(CustomerAccountAggregate.FullName) };
        var customer = RegisteredCustomer(email: EmailAddress.Create("jane@example.com"));
        customer.Anonymise(new CustomerAnonymisationDto(DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));

        var stringProperties = typeof(CustomerAccountAggregate)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(string) && p.DeclaringType == typeof(CustomerAccountAggregate));

        foreach (var property in stringProperties)
        {
            var value = (string?)property.GetValue(customer);
            if (allowList.Contains(property.Name))
            {
                Assert.Equal(CatEcommerce.Shared.Common.Constants.AnonymisationRules.PlaceholderFullName, value);
            }
            else
            {
                Assert.True(value is null, $"{property.Name} was not cleared by Anonymise: '{value}'");
            }
        }
    }
}

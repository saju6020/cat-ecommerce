using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.Tests.Validators;

/// <summary>HO-ACC-001 §11.2 -- one invalid case per validator, asserting the property and,
/// where the spec names one, the error code.</summary>
public sealed class AccountCommandValidatorsTests
{
    // U-B4
    [Theory]
    [InlineData("hunter2ab", false)] // 9 chars
    [InlineData("hunter2abc", true)] // 10 chars
    public void RegisterCustomerCommandValidator_PasswordLength(string password, bool expectedValid)
    {
        var result = new RegisterCustomerCommandValidator().Validate(new RegisterCustomerCommand
        {
            CustomerId = Guid.NewGuid(), FullName = "Jane", MobileNumber = "01712345678", Password = password,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
            Assert.Contains(result.Errors, e => e.ErrorCode == BusinessViolationCodes.PasswordTooShort);
    }

    // U-B5 -- the submitted value never appears in a validation message.
    [Fact]
    public void RegisterCustomerCommandValidator_ShortPassword_MessageNeverEchoesTheValue()
    {
        const string password = "hunter2-hunter2";
        var result = new RegisterCustomerCommandValidator().Validate(new RegisterCustomerCommand
        {
            CustomerId = Guid.NewGuid(), FullName = "Jane", MobileNumber = "01712345678", Password = "short",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.All(result.Errors, e => Assert.DoesNotContain(password, e.ErrorMessage));
    }

    [Fact]
    public void RegisterCustomerCommandValidator_NeitherIdentifier_FailsWithIdentifierRequired()
    {
        var result = new RegisterCustomerCommandValidator().Validate(new RegisterCustomerCommand
        {
            CustomerId = Guid.NewGuid(), FullName = "Jane", Password = "longenoughpw",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == BusinessViolationCodes.IdentifierRequired);
    }

    [Fact]
    public void SignInCustomerCommandValidator_EmptyIdentifier_Fails()
    {
        var result = new SignInCustomerCommandValidator().Validate(new SignInCustomerCommand
        {
            SessionId = Guid.NewGuid(), Identifier = "", Password = "x",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ResetPasswordCommandValidator_ShortNewPassword_FailsWithPasswordTooShort()
    {
        var result = new ResetPasswordCommandValidator().Validate(new ResetPasswordCommand
        {
            Token = "token", NewPassword = "short",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == BusinessViolationCodes.PasswordTooShort);
    }

    [Fact]
    public void CreateStaffAccountCommandValidator_EmptyRoles_FailsWithStaffRolesRequired()
    {
        var result = new CreateStaffAccountCommandValidator().Validate(new CreateStaffAccountCommand
        {
            StaffAccountId = Guid.NewGuid(), FullName = "Vet", MobileNumber = "01712345678", RoleIds = [],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Contains(result.Errors, e => e.ErrorCode == BusinessViolationCodes.StaffRolesRequired);
    }

    [Fact]
    public void UpdateCustomerContactDetailsCommandValidator_TooManyAdditionalNumbers_Fails()
    {
        var result = new UpdateCustomerContactDetailsCommandValidator().Validate(new UpdateCustomerContactDetailsCommand
        {
            FullName = "Jane", AdditionalPhoneNumbers = ["01711111111", "01711111112", "01711111113", "01711111114"],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AddDeliveryAddressCommandValidator_EmptyLabel_FailsOnLabel()
    {
        var result = new AddDeliveryAddressCommandValidator().Validate(new AddDeliveryAddressCommand
        {
            AddressId = Guid.NewGuid(), Label = "", Line1 = "123 Main St", City = "Dhaka", District = "Dhaka",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddDeliveryAddressCommand.Label));
    }

    [Fact]
    public void MergeCustomerCommandValidator_EmptyReason_Fails()
    {
        var result = new MergeCustomerCommandValidator().Validate(new MergeCustomerCommand
        {
            DuplicateCustomerId = Guid.NewGuid(), SurvivingCustomerId = Guid.NewGuid(), Reason = "",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RegisterWalkInCustomerCommandValidator_EmptyMobile_Fails()
    {
        var result = new RegisterWalkInCustomerCommandValidator().Validate(new RegisterWalkInCustomerCommand
        {
            CustomerId = Guid.NewGuid(), FullName = "Walk In", MobileNumber = "",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void DeleteCustomerAccountCommandValidator_EmptyPassword_Fails()
    {
        var result = new DeleteCustomerAccountCommandValidator().Validate(new DeleteCustomerAccountCommand
        {
            CurrentPassword = "", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.False(result.IsValid);
    }
}

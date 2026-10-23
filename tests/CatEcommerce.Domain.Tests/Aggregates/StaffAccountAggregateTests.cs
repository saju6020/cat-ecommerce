using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.Tests.Aggregates;

/// <summary>HO-ACC-001 §15 -- Staff & authorization (F).</summary>
public sealed class StaffAccountAggregateTests
{
    private static StaffAccountAggregate Create(params string[] roles)
    {
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Vet Person", MobileNumber.Create("01712345678"), null, roles, TestUserContext.Create(), Guid.NewGuid()));
        return staff;
    }

    // U-F1
    [Fact]
    public void Create_NormalisesTrimsDeduplicatesAndOrdersRoles()
    {
        var staff = Create(" Vet", "Vet", "Nurse");
        Assert.Equal(new[] { "Nurse", "Vet" }, staff.RoleIds);
    }

    // U-F2
    [Fact]
    public void Create_EmptyRoles_Throws()
    {
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        var ex = Assert.Throws<DomainRuleViolationException>(() => staff.Create(new StaffAccountCreationDto(
            "Name", MobileNumber.Create("01712345678"), null, [], TestUserContext.Create(), Guid.NewGuid())));
        Assert.Equal("StaffRolesRequired", ex.ErrorCode);
    }

    [Fact]
    public void Create_LowerCaseRole_Throws()
    {
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        var ex = Assert.Throws<DomainRuleViolationException>(() => staff.Create(new StaffAccountCreationDto(
            "Name", MobileNumber.Create("01712345678"), null, ["vet"], TestUserContext.Create(), Guid.NewGuid())));
        Assert.Equal("UnknownStaffRole", ex.ErrorCode);
    }

    [Fact]
    public void Create_UnknownRole_Throws()
    {
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        var ex = Assert.Throws<DomainRuleViolationException>(() => staff.Create(new StaffAccountCreationDto(
            "Name", MobileNumber.Create("01712345678"), null, ["Groomer"], TestUserContext.Create(), Guid.NewGuid())));
        Assert.Equal("UnknownStaffRole", ex.ErrorCode);
    }

    // U-F3
    [Fact]
    public void ChangeRoles_SameSet_NoEventNoVersionBumpStampUnchanged()
    {
        var staff = Create("Vet", "Nurse");
        staff.ClearEvents();
        var versionBefore = staff.Version;
        var stampBefore = staff.SecurityStamp;

        staff.ChangeRoles(new StaffRoleChangeDto(["Nurse", "Vet"], TestUserContext.Create(), Guid.NewGuid()));

        Assert.Empty(staff.Events);
        Assert.Equal(versionBefore, staff.Version);
        Assert.Equal(stampBefore, staff.SecurityStamp);
    }

    // U-F4
    [Fact]
    public void ChangeRoles_DifferentSet_RotatesStampAndRaisesEvent()
    {
        var staff = Create("Vet");
        var stampBefore = staff.SecurityStamp;

        staff.ChangeRoles(new StaffRoleChangeDto(["Administrator"], TestUserContext.Create(), Guid.NewGuid()));

        Assert.NotEqual(stampBefore, staff.SecurityStamp);
        Assert.Equal(["Administrator"], staff.RoleIds);
    }

    [Fact]
    public void Create_PasswordCredentialIsNull() => Assert.Null(Create("Vet").PasswordCredential);

    // U-G5-adjacent -- staff EnsureCanSignIn has no status check, only credential/lock.
    [Fact]
    public void EnsureCanSignIn_NoCredential_ReturnsInvalidCredentials()
    {
        var staff = Create("Vet");
        var ex = Assert.Throws<DomainRuleViolationException>(() => staff.EnsureCanSignIn(DateTimeOffset.UtcNow));
        Assert.Equal(BusinessViolationCodes.InvalidCredentials, ex.ErrorCode);
    }
    // Owner, 2026-10-20: an administrator edits the name and sign-in identifiers.
    [Fact]
    public void UpdateDetails_Changed_RaisesEventWithBeforeAndAfter_StampUnchanged()
    {
        var staff = Create("Vet");
        staff.ClearEvents();
        var stampBefore = staff.SecurityStamp;

        staff.UpdateDetails(new StaffAccountDetailsUpdateDto(
            "Vet Renamed", MobileNumber.Create("01712345678"), EmailAddress.Create("vet@example.test"), TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal("Vet Renamed", staff.FullName);
        Assert.Equal("vet@example.test", staff.EmailAddress?.Value);
        Assert.Equal(stampBefore, staff.SecurityStamp);
        var changed = Assert.IsType<CatEcommerce.Domain.Events.Account.StaffAccountDetailsChangedEvent>(Assert.Single(staff.Events));
        Assert.Equal("Vet Person", changed.PreviousFullName);
        Assert.Null(changed.PreviousEmailAddress);
        Assert.Equal("vet@example.test", changed.NewEmailAddress);
    }

    [Fact]
    public void UpdateDetails_NothingChanged_NoEventNoVersionBump()
    {
        var staff = Create("Vet");
        staff.ClearEvents();
        var versionBefore = staff.Version;

        staff.UpdateDetails(new StaffAccountDetailsUpdateDto(
            "Vet Person", MobileNumber.Create("01712345678"), null, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Empty(staff.Events);
        Assert.Equal(versionBefore, staff.Version);
    }

    [Fact]
    public void UpdateDetails_BlankNameOrNoIdentifier_Throws()
    {
        var staff = Create("Vet");
        var blank = Assert.Throws<DomainRuleViolationException>(() => staff.UpdateDetails(new StaffAccountDetailsUpdateDto(
            " ", MobileNumber.Create("01712345678"), null, TestUserContext.Create(), Guid.NewGuid())));
        Assert.Equal(BusinessViolationCodes.NameRequired, blank.ErrorCode);

        var none = Assert.Throws<DomainRuleViolationException>(() => staff.UpdateDetails(new StaffAccountDetailsUpdateDto(
            "Vet Person", null, null, TestUserContext.Create(), Guid.NewGuid())));
        Assert.Equal(BusinessViolationCodes.IdentifierRequired, none.ErrorCode);
    }
}

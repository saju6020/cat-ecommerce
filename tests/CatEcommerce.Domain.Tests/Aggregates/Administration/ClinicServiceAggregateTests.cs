using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.Aggregates.Administration;

/// <summary>
/// TEST gate seventh pass: U-D1, previously BLOCKED -- no failure case for
/// <see cref="ClinicServiceAggregate"/>'s duration bounds or role validation existed anywhere in
/// the tree; only the success path was exercised (integration-level).
/// </summary>
public sealed class ClinicServiceAggregateTests
{
    private static ClinicServiceCreateDto ValidDto(int durationMinutes = 30, IReadOnlyList<string>? qualifiedRoleIds = null) =>
        new("SVC1", "Consultation", "General consultation", ServiceCategory.Consultation, durationMinutes,
            qualifiedRoleIds ?? [StaffRoles.Vet], RoomKind.Consultation, Guid.NewGuid(), true, TestUserContext.Create(), Guid.NewGuid());

    [Theory]
    [InlineData(7)] // not a multiple of 5
    [InlineData(0)] // below the 5-minute minimum
    [InlineData(485)] // above the 480-minute maximum
    public void Create_DurationOutOfBoundsOrNotAMultipleOfFive_ThrowsInvalidServiceDuration(int durationMinutes)
    {
        var service = new ClinicServiceAggregate(Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => service.Create(ValidDto(durationMinutes: durationMinutes)));

        Assert.Equal(BusinessViolationCodes.InvalidServiceDuration, ex.ErrorCode);
    }

    [Fact]
    public void Create_NoQualifiedRoles_ThrowsServiceRolesRequired()
    {
        var service = new ClinicServiceAggregate(Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => service.Create(ValidDto(qualifiedRoleIds: [])));

        Assert.Equal(BusinessViolationCodes.ServiceRolesRequired, ex.ErrorCode);
    }

    [Fact]
    public void Create_UnknownStaffRole_ThrowsUnknownStaffRoleReferenced()
    {
        var service = new ClinicServiceAggregate(Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => service.Create(ValidDto(qualifiedRoleIds: ["NotARealRole"])));

        Assert.Equal(BusinessViolationCodes.UnknownStaffRoleReferenced, ex.ErrorCode);
    }

    [Fact]
    public void Create_ValidDuration_Succeeds()
    {
        var service = new ClinicServiceAggregate(Guid.NewGuid());

        service.Create(ValidDto(durationMinutes: 30));

        Assert.Equal(30, service.DurationMinutes);
        Assert.True(service.IsActive);
    }
}

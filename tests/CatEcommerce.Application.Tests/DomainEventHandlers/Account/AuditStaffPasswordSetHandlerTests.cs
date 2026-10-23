using CatEcommerce.Application.DomainEventHandlers.Account;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Events.Account;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.DomainEventHandlers.Account;

/// <summary>Owner, 2026-10-19 (SECURITY gate HIGH): who set a staff password is the audited actor.</summary>
public sealed class AuditStaffPasswordSetHandlerTests
{
    private static async Task<AuditEntryAggregate> AuditOf(StaffPasswordSetEvent @event)
    {
        var repository = new FakeAggregateRootRepository<AuditEntryAggregate>();
        var handler = new AuditStaffPasswordSetHandler(NullLogger<AuditStaffPasswordSetHandler>.Instance, repository,
            new FakeClientIpAccessor(), new FakeTimeProvider());
        await handler.HandleAsync(@event);
        return Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task An_administrator_setting_someone_elses_password_is_the_actor()
    {
        var staffId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var entry = await AuditOf(new StaffPasswordSetEvent
        {
            StaffAccountId = staffId, WasFirstPassword = false, SetByAdministratorId = adminId,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Equal(adminId, entry.ActorUserId);
        Assert.Equal(staffId, entry.EntityId);
        Assert.Equal("SetByAdministrator", entry.Reason);
    }

    [Fact]
    public async Task A_starting_password_given_at_creation_is_recorded_as_such()
    {
        var adminId = Guid.NewGuid();

        var entry = await AuditOf(new StaffPasswordSetEvent
        {
            StaffAccountId = Guid.NewGuid(), WasFirstPassword = true, SetByAdministratorId = adminId,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Equal(adminId, entry.ActorUserId);
        Assert.Equal("InitialPasswordByAdministrator", entry.Reason);
    }

    [Fact]
    public async Task The_staff_members_own_reset_is_still_recorded_as_theirs()
    {
        var staffId = Guid.NewGuid();

        var entry = await AuditOf(new StaffPasswordSetEvent
        {
            StaffAccountId = staffId, WasFirstPassword = false,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Equal(staffId, entry.ActorUserId);
        Assert.Equal("PasswordReset", entry.Reason);
    }
}

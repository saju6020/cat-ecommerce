using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates;

/// <summary>HO-ACC-001 §15, U-A6/U-A7.</summary>
public sealed class AuditEntryAggregateTests
{
    private static AuditEntryDto ValidDto() => new(
        "CustomerAccount", Guid.NewGuid(), "CustomerSignedIn", "SignInSucceeded", Guid.NewGuid(),
        DateTimeOffset.UtcNow, null, null, TestUserContext.Create(), Guid.NewGuid(),
        AuditCategory.AuthenticationEvent, null);

    // U-A6
    [Fact]
    public void Record_Twice_Throws()
    {
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(ValidDto());

        var ex = Assert.Throws<DomainRuleViolationException>(() => entry.Record(ValidDto()));
        Assert.Equal("AuditEntryAlreadyRecorded", ex.ErrorCode);
    }

    [Fact]
    public void Record_BlankReason_Throws()
    {
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        var dto = new AuditEntryDto(
            "CustomerAccount", Guid.NewGuid(), "Action", "  ", Guid.NewGuid(),
            DateTimeOffset.UtcNow, null, null, TestUserContext.Create(), Guid.NewGuid(),
            AuditCategory.AuthenticationEvent, null);

        var ex = Assert.Throws<DomainRuleViolationException>(() => entry.Record(dto));
        Assert.Equal("InvalidAuditEntry", ex.ErrorCode);
    }

    // U-A7
    [Fact]
    public void Aggregate_RecordIsTheOnlyPublicMutator()
    {
        var mutatorNames = typeof(AuditEntryAggregate)
            .GetMethods()
            .Where(m => m.DeclaringType == typeof(AuditEntryAggregate) && !m.IsSpecialName)
            .Select(m => m.Name)
            .Distinct()
            .ToList();

        Assert.Equal(["Record"], mutatorNames);
    }
}

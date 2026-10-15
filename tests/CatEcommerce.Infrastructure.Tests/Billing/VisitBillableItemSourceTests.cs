using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Billing.ItemSources;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Billing;

/// <summary>
/// HO-BIL-001 Phase J TEST gate (owner decision "old visits: bill by hand"). The earlier test of this rule (PhaseJCaptureTests) feeds the capture handler a
/// fake source that already says "not billable", so it passed whatever the real visit source did. These drive the REAL <see cref="VisitBillableItemSource"/>.
/// Each refusal happens before the source touches the database, so the context is never needed and a test that did reach it would fail loudly (null).
/// </summary>
public sealed class VisitBillableItemSourceTests
{
    private sealed class FakeReader(VisitBillingFacts? facts) : IVisitBillingReader
    {
        public Task<VisitBillingFacts?> GetAsync(Guid visitId, CancellationToken ct = default) => Task.FromResult(facts);
    }

    private static VisitBillingFacts Facts(Guid? customerId, bool finalised, Guid branchId) =>
        new(Guid.NewGuid(), branchId, Guid.NewGuid(), Guid.NewGuid(), customerId, finalised, finalised ? DateTimeOffset.UtcNow : null, []);

    private static VisitBillableItemSource Source(VisitBillingFacts? facts) => new(new FakeReader(facts), null!);

    [Fact]
    public async Task AVisitFinalisedBeforeTheOwnerWasStored_IsNotBillable_AndIsNeverBilledToAnyoneElse()
    {
        var branch = Guid.NewGuid();

        var result = await Source(Facts(customerId: null, finalised: true, branch)).GetItemsAsync(Guid.NewGuid());

        Assert.True(result.Exists);
        Assert.False(result.IsBillable);
        Assert.Equal("VisitCustomerNotRecorded", result.Reason);
        Assert.Empty(result.Items);
        Assert.Equal(branch, result.BranchId); // its own branch is told why; the capture hides it from any other
    }

    [Fact]
    public async Task AnOpenVisit_IsNotBillable_EvenWithAnOwnerOnFile()
    {
        var result = await Source(Facts(Guid.NewGuid(), finalised: false, Guid.NewGuid())).GetItemsAsync(Guid.NewGuid());

        Assert.True(result.Exists);
        Assert.False(result.IsBillable);
        Assert.Equal("VisitNotFinalised", result.Reason);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task AnUnknownVisit_DoesNotExist()
    {
        var result = await Source(null).GetItemsAsync(Guid.NewGuid());

        Assert.False(result.Exists);
        Assert.Empty(result.Items);
    }
}

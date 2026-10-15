using CatEcommerce.Read.QueryHandlers.Operations;
using CatEcommerce.Read.ViewModels;

namespace CatEcommerce.Application.Tests.QueryHandlers.Operations;

/// <summary>HO-XCT-001 U-D1, U-D2 -- the snapshot ETag depends on content only (section 12.4).</summary>
public sealed class OfflineSnapshotEtagTests
{
    private static readonly SnapshotBranch Branch = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Main clinic", "Asia/Dhaka");
    private static readonly DateOnly Day = new(2026, 10, 12);
    private static readonly Guid AppointmentId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static IReadOnlyDictionary<string, object> Sections(string status) => new SortedDictionary<string, object>
    {
        ["appointments"] = new SnapshotAppointmentsSection([new SnapshotAppointmentItem(
            AppointmentId, new DateTimeOffset(2026, 10, 12, 6, 0, 0, TimeSpan.Zero), null, status, false, null, "Mitu", false, null, "Customer", null, "Vaccination", null, null)]),
    };

    private static readonly IReadOnlyList<SnapshotOmittedSection> Omitted = [new("boardingTasks", "NotAvailable")];

    [Fact]
    public void U_D1_TheSameContent_GivesTheSameETag_AndItIsAQuotedStrongHash()
    {
        var first = GetFrontDeskOfflineSnapshotQueryHandler.ComputeETag(Branch, Day, Sections("Booked"), Omitted);
        var second = GetFrontDeskOfflineSnapshotQueryHandler.ComputeETag(Branch, Day, Sections("Booked"), Omitted);

        Assert.Equal(first, second);
        Assert.Matches("^\"[0-9a-f]{64}\"$", first);
    }

    [Fact]
    public void U_D2_OneStatusChange_GivesADifferentETag()
    {
        Assert.NotEqual(
            GetFrontDeskOfflineSnapshotQueryHandler.ComputeETag(Branch, Day, Sections("Booked"), Omitted),
            GetFrontDeskOfflineSnapshotQueryHandler.ComputeETag(Branch, Day, Sections("CheckedIn"), Omitted));
    }

    [Fact]
    public void AnOmittedSectionChangesTheETag()
    {
        Assert.NotEqual(
            GetFrontDeskOfflineSnapshotQueryHandler.ComputeETag(Branch, Day, Sections("Booked"), Omitted),
            GetFrontDeskOfflineSnapshotQueryHandler.ComputeETag(Branch, Day, Sections("Booked"), [new("boardingTasks", "NotPermitted")]));
    }
}

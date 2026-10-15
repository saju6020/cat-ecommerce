using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-RPT-001 section 15 E -- U-E1 and U-E3: the tile set per role. Pure.</summary>
public sealed class DashboardTilesTests
{
    private static string[] Keys(params string[] roles) => DashboardTiles.ForRoles(roles).Select(t => t.Key).ToArray();

    [Fact]
    public void U_E1_EveryRole_GetsTheTilesOfSection811()
    {
        Assert.Equal(["clinic.appointments-today", "clinic.queue-now", "clinic.vaccinations-due", "surgery.today"], Keys(StaffRoles.Reception));
        Assert.Equal(["stock.expiry", "stock.low", "stock.consignments-open"], Keys(StaffRoles.Inventory));
        Assert.Equal(["orders.today", "orders.awaiting-transfer", "orders.returns-pending", "revenue.shop-today"], Keys(StaffRoles.ShopManager));
        Assert.Equal(["orders.awaiting-transfer", "payments.today", "payments.unreconciled", "billing.cash-up", "billing.receivables"], Keys(StaffRoles.Accountant));
        Assert.Equal(["clinic.appointments-today", "clinic.queue-now", "clinic.my-appointments-today", "clinic.consultations-unfinalised", "surgery.today"], Keys(StaffRoles.Vet));
    }

    [Fact]
    public void U_E1_ForAVetWhoIsAlsoAnAdministrator_TheUnionIsDeduplicated_InTheOrderOfTheFirstRoleInStaffRolesOrder()
    {
        var keys = Keys(StaffRoles.Administrator, StaffRoles.Vet); // the roles are ordered by StaffRoles.All, so Vet comes first whatever the order given

        Assert.Equal(keys.Distinct().Count(), keys.Length);
        Assert.Equal("clinic.appointments-today", keys[0]);
        Assert.Contains("payments.today", keys);
        Assert.Contains("clinic.my-appointments-today", keys);
        Assert.True(Array.IndexOf(keys, "clinic.my-appointments-today") < Array.IndexOf(keys, "stock.expiry"));
    }

    [Fact]
    public void U_E3_ANurse_GetsClinicTilesOnly_NoStockSalesPaymentOrRevenueTile()
    {
        var keys = Keys(StaffRoles.Nurse);

        Assert.NotEmpty(keys);
        Assert.All(keys, k => Assert.True(k.StartsWith("clinic.", StringComparison.Ordinal) || k.StartsWith("surgery.", StringComparison.Ordinal), k));
        Assert.All(DashboardTiles.ForRole(StaffRoles.Nurse), t => Assert.False(ReportAccessPolicy.Holds([StaffRoles.Nurse], Actions.Reports.Any)));
    }

    [Fact]
    public void U_E4_NoBoardingTileIsListed_BecauseItsModuleIsNotBuilt_AndEveryTileNamesAGateAction()
    {
        Assert.DoesNotContain(DashboardTiles.All, t => t.Key.StartsWith("boarding.", StringComparison.Ordinal));
        Assert.Equal(18, DashboardTiles.All.Count);
        Assert.All(DashboardTiles.All, t => Assert.True(ActionRoles.TryGetRoles(t.GateAction, out _), t.Key));
        Assert.Equal(DashboardTiles.All.Count, DashboardTiles.All.Select(t => t.Key).Distinct().Count());
    }
}

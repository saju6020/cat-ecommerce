namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Reporting's own rows in the solution-wide action-to-role table (HO-RPT-001 section 8.8 / 12.3, ADR-281). Appendix D's "Reports" row: Reception R (ops), Nurse -, Vet R (clinical),
/// Caretaker R (boarding), Inventory R (stock), Shop Manager R (sales), Accountant F (financial) and, by owner decision 2026-10-13, sales too, Admin F. The Vet also holds the clinic-operations category (OQ-7: SRS section 8.1
/// names the Vet as a primary user of the daily schedule). The Nurse holds no report action at all, so is outside <c>Reports.Any</c>.
/// </summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] ReportsRows =
    [
        (Actions.Reports.ClinicOperationsView, [StaffRoles.Reception, StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Reports.ClinicalView, [StaffRoles.Vet, StaffRoles.Administrator]),
        (Actions.Reports.BoardingView, [StaffRoles.Caretaker, StaffRoles.Administrator]),
        (Actions.Reports.StockView, [StaffRoles.Inventory, StaffRoles.Administrator]),
        (Actions.Reports.SalesView, [StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Reports.FinancialView, [StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Reports.GrowthView, [StaffRoles.Administrator]),
        (Actions.Reports.Any, [StaffRoles.Reception, StaffRoles.Vet, StaffRoles.Caretaker, StaffRoles.Inventory, StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Reports.Schedule, [StaffRoles.Administrator]),
        (Actions.Dashboard.View, [StaffRoles.Reception, StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Caretaker, StaffRoles.Inventory, StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
    ];
}

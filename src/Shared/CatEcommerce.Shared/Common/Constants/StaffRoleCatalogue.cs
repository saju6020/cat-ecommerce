using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>One cell of the Appendix D matrix -- HO-ACC-001 §10.2.</summary>
public sealed record StaffCapabilityCell(string Capability, string RoleId, CapabilityAccess Access, string? Qualifier = null);

/// <summary>
/// The twenty Appendix D capability rows (`docs/SRS.md` Appendix D) x the eight staff roles
/// (<see cref="StaffRoles"/>), as read-only code-defined data (ADR-036) -- served by
/// <c>GET /api/staff-roles</c>. The <c>Customer</c> column of Appendix D is not represented:
/// <c>Customer</c> is not a staff role.
/// </summary>
public static class StaffRoleCatalogue
{
    public const string OwnCatProfile = "Own cat profile";
    public const string AnyCatProfile = "Any cat profile";
    public const string Appointments = "Appointments";
    public const string ClinicalNotes = "Clinical notes";
    public const string Prescribe = "Prescribe";
    public const string DispenseMedicine = "Dispense medicine";
    public const string ControlledDrugRegister = "Controlled drug register";
    public const string BoardingBookings = "Boarding bookings";
    public const string CareLogsAndTasks = "Care logs & tasks";
    public const string Incidents = "Incidents";
    public const string ProductCatalogue = "Product catalogue";
    public const string StockAndBatches = "Stock & batches";
    public const string PurchaseOrdersImports = "Purchase orders / imports";
    public const string OrdersAndFulfilment = "Orders & fulfilment";
    public const string InvoicesAndPayments = "Invoices & payments";
    public const string RefundsAndCreditNotes = "Refunds & credit notes";
    public const string Reports = "Reports";
    public const string StaffAndPermissions = "Staff & permissions";
    public const string SystemConfiguration = "System configuration";
    public const string AuditLog = "Audit log";

    public static readonly IReadOnlyList<string> Capabilities =
    [
        OwnCatProfile, AnyCatProfile, Appointments, ClinicalNotes, Prescribe, DispenseMedicine,
        ControlledDrugRegister, BoardingBookings, CareLogsAndTasks, Incidents, ProductCatalogue,
        StockAndBatches, PurchaseOrdersImports, OrdersAndFulfilment, InvoicesAndPayments,
        RefundsAndCreditNotes, Reports, StaffAndPermissions, SystemConfiguration, AuditLog
    ];

    private static readonly CapabilityAccess None = CapabilityAccess.None;
    private static readonly CapabilityAccess R = CapabilityAccess.ReadOnly;
    private static readonly CapabilityAccess W = CapabilityAccess.CreateEdit;
    private static readonly CapabilityAccess F = CapabilityAccess.Full;

    /// <summary>Row(capability, Reception, Nurse, Vet, Caretaker, Inventory, ShopManager, Accountant, Administrator).</summary>
    private static readonly (string Capability, CapabilityAccess Reception, CapabilityAccess Nurse, CapabilityAccess Vet,
        CapabilityAccess Caretaker, CapabilityAccess Inventory, CapabilityAccess ShopManager,
        CapabilityAccess Accountant, CapabilityAccess Administrator, string? Qualifier)[] Rows =
    [
        (OwnCatProfile, W, W, W, R, None, None, None, F, null),
        (AnyCatProfile, W, W, W, R, None, None, None, F, null),
        (Appointments, F, R, W, None, None, None, None, F, null),
        (ClinicalNotes, None, W, F, None, None, None, None, R, null),
        (Prescribe, None, None, F, None, None, None, None, None, null),
        (DispenseMedicine, W, W, F, None, R, None, None, F, null),
        (ControlledDrugRegister, None, R, W, None, R, None, None, R, null),
        (BoardingBookings, F, None, R, R, None, None, None, F, null),
        (CareLogsAndTasks, R, W, R, F, None, None, None, F, null),
        (Incidents, R, W, W, W, None, None, None, F, null),
        (ProductCatalogue, R, None, None, None, R, F, None, F, null),
        (StockAndBatches, R, R, R, R, F, R, None, F, null),
        (PurchaseOrdersImports, None, None, None, None, F, R, R, F, null),
        (OrdersAndFulfilment, W, None, None, None, R, F, R, F, null),
        (InvoicesAndPayments, W, None, None, None, None, W, F, F, null),
        (RefundsAndCreditNotes, None, None, None, None, None, W, F, F, null),
        (StaffAndPermissions, None, None, None, None, None, None, None, F, null),
        (SystemConfiguration, None, None, None, None, None, None, None, F, null),
        (AuditLog, None, None, None, None, None, None, R, F, null),
    ];

    // "Reports" carries a per-role qualifier (ops/clinical/boarding/stock/sales/financial), so it
    // is expanded separately rather than forced into the uniform tuple shape above.
    private static readonly (string RoleId, CapabilityAccess Access, string? Qualifier)[] ReportsRow =
    [
        (StaffRoles.Reception, R, "ops"),
        (StaffRoles.Nurse, None, null),
        (StaffRoles.Vet, R, "clinical"),
        (StaffRoles.Caretaker, R, "boarding"),
        (StaffRoles.Inventory, R, "stock"),
        (StaffRoles.ShopManager, R, "sales"),
        (StaffRoles.Accountant, F, "financial"),
        (StaffRoles.Administrator, F, null),
    ];

    /// <summary>All 20 x 8 = 160 cells.</summary>
    public static readonly IReadOnlyList<StaffCapabilityCell> Cells = BuildCells();

    private static List<StaffCapabilityCell> BuildCells()
    {
        var cells = new List<StaffCapabilityCell>();

        foreach (var row in Rows)
        {
            cells.Add(new StaffCapabilityCell(row.Capability, StaffRoles.Reception, row.Reception));
            cells.Add(new StaffCapabilityCell(row.Capability, StaffRoles.Nurse, row.Nurse));
            cells.Add(new StaffCapabilityCell(row.Capability, StaffRoles.Vet, row.Vet));
            cells.Add(new StaffCapabilityCell(row.Capability, StaffRoles.Caretaker, row.Caretaker));
            cells.Add(new StaffCapabilityCell(row.Capability, StaffRoles.Inventory, row.Inventory));
            cells.Add(new StaffCapabilityCell(row.Capability, StaffRoles.ShopManager, row.ShopManager));
            cells.Add(new StaffCapabilityCell(row.Capability, StaffRoles.Accountant, row.Accountant));
            cells.Add(new StaffCapabilityCell(row.Capability, StaffRoles.Administrator, row.Administrator));
        }

        foreach (var (roleId, access, qualifier) in ReportsRow)
            cells.Add(new StaffCapabilityCell(Reports, roleId, access, qualifier));

        return cells;
    }
}

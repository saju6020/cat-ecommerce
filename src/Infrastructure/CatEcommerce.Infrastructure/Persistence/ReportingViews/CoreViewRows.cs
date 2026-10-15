namespace CatEcommerce.Infrastructure.Persistence.ReportingViews;

// HO-RPT-001 section 8.4 -- the keyless row types of the phase A views. Properties map to the snake_case column of the same name (ReportViewConfiguration). No float or double anywhere (U-D4).

public sealed class RvStaffRow
{
    public Guid StaffAccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string[] RoleIds { get; set; } = [];
    public bool EmailPresent { get; set; }
}

public sealed class RvCustomerRow
{
    public Guid CustomerId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Status { get; set; }
    public int RegistrationChannel { get; set; }
    public DateTimeOffset RegisteredOn { get; set; }
    public Guid? MergedIntoCustomerId { get; set; }
}

public sealed class RvBranchRow
{
    public Guid BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class RvStaffBranchRow
{
    public Guid StaffAccountId { get; set; }
    public Guid BranchId { get; set; }
    public bool IsPrimary { get; set; }
}

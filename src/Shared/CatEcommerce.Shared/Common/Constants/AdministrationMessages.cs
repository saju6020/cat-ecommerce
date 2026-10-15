namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-ADM-001 §10.3. Shared message constants for Administration responses.</summary>
public static class AdministrationMessages
{
    public const string DeliveryTaxClassRequired = "The DELIVERY tax class carries the tax on delivery charges and cannot be deactivated. Change its rate instead.";
    public const string MaintenanceMessageDefault = "The site is temporarily unavailable for maintenance. Please try again shortly.";
    public const string AuditExportTooLarge = "This filter matches too many rows to export. Narrow the date range or add more filters.";
}

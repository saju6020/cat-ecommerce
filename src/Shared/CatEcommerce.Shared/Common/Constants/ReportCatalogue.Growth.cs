using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-RPT-001 section 8.3 phase F -- R18 to R20 (US-RPT-04, SRS section 8.3). R21 (subscription health, HO-SUB-001), R22 (cross-line conversion, which needs the boarding line) and R23 (cart abandonment, whose reminders
/// HO-ORD-001 has not built) are <b>not delivered yet</b> and are therefore absent from the catalogue, never present with empty rows (section 17).
/// </summary>
public static partial class ReportCatalogue
{
    public const string CustomersNewVsReturningKey = "customers-new-vs-returning";
    public const string CustomerLifetimeValueKey = "customer-lifetime-value";
    public const string CohortRetentionKey = "cohort-retention";

    private static readonly IReadOnlyList<ReportDefinition> GrowthDefinitions =
    [
        // R18 -- the definitions of ADR-291.
        new ReportDefinition(
            CustomersNewVsReturningKey, "New and returning customers",
            "Per period: customers with an issued invoice, how many of them were new (their first invoice ever fell in the period) and how many returned, by how they registered, and registrations.",
            ReportCategory.Growth, ReportKind.Period, ReportRelativePeriod.PreviousMonth, 731, [],
            [
                new ReportColumn("bucket", "Period", ReportColumnType.Text),
                new ReportColumn("activeCustomers", "Active customers", ReportColumnType.Integer),
                new ReportColumn("newCustomers", "New", ReportColumnType.Integer),
                new ReportColumn("returningCustomers", "Returning", ReportColumnType.Integer),
                new ReportColumn("newSelfService", "New: self-service", ReportColumnType.Integer),
                new ReportColumn("newReception", "New: at reception", ReportColumnType.Integer),
                new ReportColumn("registrations", "Registrations", ReportColumnType.Integer),
            ],
            "bucket", true, true, ["BIL", "ACC"]),

        // R19 -- lifetime value across the lines, for customers first seen in the window.
        new ReportDefinition(
            CustomerLifetimeValueKey, "Customer lifetime value",
            "For customers whose first invoice fell in the range: their net revenue to the end of the range across clinic, shop and boarding, in value bands, and the fifty highest.",
            ReportCategory.Growth, ReportKind.Period, ReportRelativePeriod.PreviousMonth, 731, [],
            [
                new ReportColumn("section", "Section", ReportColumnType.Text),
                new ReportColumn("label", "Band or customer", ReportColumnType.Text),
                new ReportColumn("customers", "Customers", ReportColumnType.Integer),
                new ReportColumn("averageLtv", "Average lifetime value", ReportColumnType.Money),
                new ReportColumn("clinicShare", "Clinic share %", ReportColumnType.Percent),
                new ReportColumn("shopShare", "Shop share %", ReportColumnType.Percent),
                new ReportColumn("boardingShare", "Boarding share %", ReportColumnType.Percent),
                new ReportColumn("linesUsed", "Lines used", ReportColumnType.Text),
            ],
            null, false, false, ["BIL", "ACC"]),

        // R20 -- monthly cohorts, months 1 to 12 (OQ-8).
        new ReportDefinition(
            CohortRetentionKey, "Cohort retention",
            "Monthly cohorts by the month of the customer's first invoice, and the percentage still buying in each of the next twelve months. A month that has not happened yet is blank.",
            ReportCategory.Growth, ReportKind.Period, ReportRelativePeriod.PreviousMonth, 731,
            [new ReportParameter("measure", "Retained means", ReportParameterType.Enum, "AnyLine", ["AnyLine", "RepeatPurchase", "RepeatVisit"])],
            [
                new ReportColumn("cohortMonth", "Cohort month", ReportColumnType.Text),
                new ReportColumn("cohortSize", "Cohort size", ReportColumnType.Integer),
                .. Enumerable.Range(1, 12).Select(m => new ReportColumn($"month{m}", $"Month {m} %", ReportColumnType.Percent)),
            ],
            "cohortMonth", false, false, ["BIL", "ACC"]),
    ];
}

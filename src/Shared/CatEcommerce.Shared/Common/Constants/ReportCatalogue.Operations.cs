using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-RPT-001 section 8.3 phase H -- R27 only. R24 (boarding occupancy), R25 (care compliance) and R26 (incidents) read HO-BRD-001, which is not merged, so they are <b>not delivered yet</b> and absent from the
/// catalogue, never present with empty rows.
/// </summary>
public static partial class ReportCatalogue
{
    public const string OrderFulfilmentKey = "order-fulfilment";

    private static readonly IReadOnlyList<ReportDefinition> OperationsDefinitions =
    [
        new ReportDefinition(
            OrderFulfilmentKey, "Order fulfilment",
            "Every order placed in the range with its age, whether it breached the dispatch service level (24 hours from confirmation to dispatch), and when it was dispatched and delivered.",
            ReportCategory.Sales, ReportKind.Period, ReportRelativePeriod.Last7Days, 366, [],
            [
                new ReportColumn("orderNumber", "Order", ReportColumnType.Text),
                new ReportColumn("placedOn", "Placed", ReportColumnType.Instant),
                new ReportColumn("channel", "Channel", ReportColumnType.Text),
                new ReportColumn("status", "Status", ReportColumnType.Text),
                new ReportColumn("ageHours", "Age (hours)", ReportColumnType.Integer, "h"),
                new ReportColumn("slaBreach", "Dispatch SLA breached", ReportColumnType.Boolean),
                new ReportColumn("dispatchedOn", "Dispatched", ReportColumnType.Instant),
                new ReportColumn("deliveredOn", "Delivered", ReportColumnType.Instant),
            ],
            "-placedOn", true, false, ["ORD"]),
    ];
}

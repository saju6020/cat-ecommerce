using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Notifications (HO-NTF-001) table classifications (HO-XCT-001 §5.2 C-ALL-1). Per HO-XCT-001
/// §10.3: "every notify.* log and request table" is <c>Notification</c>/<c>Forbidden</c>; "notify.*
/// template tables" are <c>Operational</c>/<c>Forbidden</c>.
///
/// Gate fix 2026-09-27 (ARCHITECTURE MEDIUM, C-ALL-1): this file previously classed
/// <c>notification_template</c> as <c>Notification</c> (it is a template table -- §10.3 says
/// <c>Operational</c>) and <c>notification_attempt</c> as <c>HardDeletePolicy.AggregateOnly</c> (it
/// is a log table -- §10.3 says <c>Forbidden</c>, not the usual "owned child follows its
/// aggregate's policy" default). Both corrected below.
/// </summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> NotificationsEntries =
    [
        new TableClassification("notify", "notification_template", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3 -- \"notify.* template tables\": Operational, Forbidden"),

        // Deliberate exception to the blanket "notify.* template tables -> Forbidden" rule:
        // NotificationTemplateAggregate.ResetToDefault (§8.2) wholesale-replaces this owned
        // collection -- _contents.Clear() then re-adds -- which is exactly the "an owned collection
        // an aggregate method replaces wholesale" carve-out in C-ALL-1's own ordered rules (the
        // CurrentState pattern), not a log or a request row. AggregateOnly (follow the owner's own
        // per-row lifecycle) is therefore the correct policy for this specific child table, even
        // though it differs from the header row's own Forbidden.
        new TableClassification("notify", "notification_template_content", RecordClass.Notification, HardDeletePolicy.AggregateOnly, "Owned by notification_template; ResetToDefault wholesale-replaces the collection (the CurrentState carve-out in C-ALL-1's ordered rules), not append-only history"),

        new TableClassification("notify", "notification_request", RecordClass.Notification, HardDeletePolicy.Forbidden, "HO-NTF-001 §8.4 -- the outbox item, never deleted (ADR-056)"),
        new TableClassification("notify", "notification", RecordClass.Notification, HardDeletePolicy.Forbidden, "HO-NTF-001 §8.6 -- the delivery log and inbox item (US-NTF-06)"),
        new TableClassification("notify", "notification_attempt", RecordClass.Notification, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3 -- \"every notify.* log and request table\": Notification, Forbidden. Append-only delivery-attempt history, not a CurrentState collection"),
    ];
}

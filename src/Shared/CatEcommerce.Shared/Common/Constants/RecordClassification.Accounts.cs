using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Accounts (HO-ACC-001) table classifications. Every entry and its <c>Basis</c> are transcribed
/// verbatim from HO-XCT-001 §10.3's own table -- §10.3 already classifies every table Accounts
/// owns (the 7 aggregate roots' tables plus the 2 owned-entity tables they contribute), so there
/// is no gap here. Table names transcribed from the merged EF configurations
/// (<c>CustomerAccountConfiguration</c>, <c>CustomerAddressBookConfiguration</c>,
/// <c>CustomerNotificationPreferencesConfiguration</c>, <c>CustomerSessionConfiguration</c>,
/// <c>StaffAccountConfiguration</c>, <c>StaffSessionConfiguration</c>,
/// <c>PasswordResetTokenConfiguration</c>).
///
/// <c>audit.audit_entry</c> is also classified here, not by Cat Profiles, per §10.3 ("Audit |
/// Forbidden (HO-ACC-001's triggers, F33) | D-5") and ADR-341 (only the owning area classifies a
/// table) -- this closes the intentional, temporary gap <c>RecordClassification.CatProfiles.cs</c>
/// left open until Accounts landed. The two immutability triggers enforcing "Forbidden" here are
/// <c>CatEcommerceDbContext.Accounts.cs</c>'s migration (F33).
///
/// The three Security tables (customer_session, staff_session, password_reset_token) have no
/// per-table citation in §10.3's own table (the Basis column is blank for that row); their Basis
/// here is transcribed from §8.2's general rule for the Security class instead, not invented.
/// </summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> AccountsEntries =
    [
        new TableClassification("account", "customer_account", RecordClass.CustomerPersonal, HardDeletePolicy.Forbidden, "HO-ACC-001 ADR-041"),

        new TableClassification("account", "customer_address_book", RecordClass.CustomerPersonal, HardDeletePolicy.AggregateOnly, "Clear / remove address"),
        new TableClassification("account", "customer_delivery_address", RecordClass.CustomerPersonal, HardDeletePolicy.AggregateOnly, "Clear / remove address"),
        new TableClassification("account", "customer_notification_preferences", RecordClass.CustomerPersonal, HardDeletePolicy.AggregateOnly, "Clear / remove address"),
        new TableClassification("account", "customer_marketing_consent", RecordClass.CustomerPersonal, HardDeletePolicy.AggregateOnly, "Clear / remove address"),

        new TableClassification("account", "customer_session", RecordClass.Security, HardDeletePolicy.Purgeable, "§8.2 Security: purged retention.securityArtefactDays after expiry or end"),
        new TableClassification("account", "staff_session", RecordClass.Security, HardDeletePolicy.Purgeable, "§8.2 Security: purged retention.securityArtefactDays after expiry or end"),
        new TableClassification("account", "password_reset_token", RecordClass.Security, HardDeletePolicy.Purgeable, "§8.2 Security: purged retention.securityArtefactDays after expiry or end"),

        new TableClassification("account", "staff_account", RecordClass.Operational, HardDeletePolicy.Forbidden, "C-9 attribution"),

        new TableClassification("audit", "audit_entry", RecordClass.Audit, HardDeletePolicy.Forbidden, "D-5; enforced by HO-ACC-001's own triggers (F33)"),
    ];
}

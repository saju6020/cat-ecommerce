using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>
/// HO-XCT-001 §8.2/§10.3, ADR-298, ADR-341. Exercises the reflection-based aggregation across
/// per-area partial files (currently only <c>RecordClassification.CatProfiles.cs</c>) and the
/// classifications it contributes.
/// </summary>
public sealed class RecordClassificationTests
{
    [Fact]
    public void All_IncludesEveryCatProfilesTable()
    {
        var tables = RecordClassification.All
            .Where(entry => entry.Schema == "clinic")
            .Select(entry => entry.Table)
            .ToList();

        Assert.Contains("cat", tables);
        Assert.Contains("cat_vaccination", tables);
        Assert.Contains("cat_parasite_treatment", tables);
        Assert.Contains("cat_weight_record", tables);
        Assert.Contains("cat_allergy", tables);
        Assert.Contains("cat_chronic_condition", tables);
        Assert.Contains("cat_medication", tables);
        Assert.Contains("cat_photo", tables);
        Assert.Contains("vaccine", tables);
    }

    [Fact]
    public void All_HasNoDuplicateSchemaTablePairs()
    {
        var duplicates = RecordClassification.All
            .GroupBy(entry => (entry.Schema, entry.Table))
            .Where(group => group.Count() > 1)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Find_KnownTable_ReturnsItsClassification()
    {
        var classification = RecordClassification.Find("clinic", "cat");

        Assert.NotNull(classification);
        Assert.Equal(RecordClass.Clinical, classification!.RecordClass);
        Assert.Equal(HardDeletePolicy.Forbidden, classification.HardDeletePolicy);
        Assert.Equal("D-8", classification.Basis);
    }

    [Fact]
    public void Find_CurrentStateOwnedCollection_IsAggregateOnly()
    {
        var classification = RecordClassification.Find("clinic", "cat_medication");

        Assert.NotNull(classification);
        Assert.Equal(RecordClass.CurrentState, classification!.RecordClass);
        Assert.Equal(HardDeletePolicy.AggregateOnly, classification.HardDeletePolicy);
    }

    [Fact]
    public void Find_UnknownTable_ReturnsNull()
    {
        Assert.Null(RecordClassification.Find("clinic", "does_not_exist"));
    }

    [Fact]
    public void Find_AuditEntry_IsAnIntentionalGapUntilAccountsLands()
    {
        // audit.audit_entry is mapped in the current EF model (Cat Profiles writes to it
        // directly, and Accounts writes to it too), but it is an Accounts-context table
        // (HO-XCT-001 §10.3) and is classified by RecordClassification.Accounts.cs, not by
        // Cat Profiles (ADR-341). Accounts has now landed, so the gap is closed.
        var classification = RecordClassification.Find("audit", "audit_entry");

        Assert.NotNull(classification);
        Assert.Equal(RecordClass.Audit, classification!.RecordClass);
        Assert.Equal(HardDeletePolicy.Forbidden, classification.HardDeletePolicy);
        Assert.Equal("D-5; enforced by HO-ACC-001's own triggers (F33)", classification.Basis);
    }

    [Fact]
    public void All_IncludesEveryAccountsTable()
    {
        var tables = RecordClassification.All
            .Where(entry => entry.Schema == "account")
            .Select(entry => entry.Table)
            .ToList();

        Assert.Contains("customer_account", tables);
        Assert.Contains("customer_address_book", tables);
        Assert.Contains("customer_delivery_address", tables);
        Assert.Contains("customer_notification_preferences", tables);
        Assert.Contains("customer_marketing_consent", tables);
        Assert.Contains("customer_session", tables);
        Assert.Contains("staff_session", tables);
        Assert.Contains("password_reset_token", tables);
        Assert.Contains("staff_account", tables);
    }

    [Fact]
    public void Find_CustomerAccount_IsCustomerPersonalForbidden()
    {
        var classification = RecordClassification.Find("account", "customer_account");

        Assert.NotNull(classification);
        Assert.Equal(RecordClass.CustomerPersonal, classification!.RecordClass);
        Assert.Equal(HardDeletePolicy.Forbidden, classification.HardDeletePolicy);
    }

    [Fact]
    public void Find_SessionAndResetTokenTables_ArePurgeableSecurity()
    {
        foreach (var table in new[] { "customer_session", "staff_session", "password_reset_token" })
        {
            var classification = RecordClassification.Find("account", table);

            Assert.NotNull(classification);
            Assert.Equal(RecordClass.Security, classification!.RecordClass);
            Assert.Equal(HardDeletePolicy.Purgeable, classification.HardDeletePolicy);
        }
    }

    /// <summary>HO-NTF-001 ARCHITECTURE gate fix (2026-09-27, MEDIUM, C-ALL-1). HO-XCT-001 §10.3:
    /// "notify.* template tables" are Operational/Forbidden.</summary>
    [Fact]
    public void Find_NotificationTemplate_IsOperationalForbidden()
    {
        var classification = RecordClassification.Find("notify", "notification_template");

        Assert.NotNull(classification);
        Assert.Equal(RecordClass.Operational, classification!.RecordClass);
        Assert.Equal(HardDeletePolicy.Forbidden, classification.HardDeletePolicy);
    }

    /// <summary>HO-NTF-001 ARCHITECTURE gate fix (2026-09-27, MEDIUM, C-ALL-1). HO-XCT-001 §10.3:
    /// "every notify.* log and request table" is Notification/Forbidden -- notification_attempt is
    /// append-only delivery-attempt history, not a wholesale-replaced owned collection.</summary>
    [Fact]
    public void Find_NotificationAttempt_IsNotificationForbidden()
    {
        var classification = RecordClassification.Find("notify", "notification_attempt");

        Assert.NotNull(classification);
        Assert.Equal(RecordClass.Notification, classification!.RecordClass);
        Assert.Equal(HardDeletePolicy.Forbidden, classification.HardDeletePolicy);
    }

    [Fact]
    public void All_IncludesEveryNotificationTable()
    {
        var tables = RecordClassification.All
            .Where(entry => entry.Schema == "notify")
            .Select(entry => entry.Table)
            .ToList();

        Assert.Contains("notification_template", tables);
        Assert.Contains("notification_template_content", tables);
        Assert.Contains("notification_request", tables);
        Assert.Contains("notification", tables);
        Assert.Contains("notification_attempt", tables);
    }
}

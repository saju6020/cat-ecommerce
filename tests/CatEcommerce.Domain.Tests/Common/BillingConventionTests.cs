using System.Reflection;
using System.Text.RegularExpressions;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-BIL-001 §15 group X -- the cross-cutting rules that fail the build rather than rely on review
/// (U-X1, U-X2, U-X4, U-X5, and the classification and violation-code duties of CONTRACTS.md C-ALL-1).</summary>
public sealed class BillingConventionTests
{
    // ── U-X1 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_X1_NoBillingEventCarriesAStringProperty()
    {
        var eventTypes = typeof(InvoiceIssuedEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(InvoiceIssuedEvent).Namespace && typeof(Event).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(eventTypes);

        var violations = eventTypes
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        Assert.True(violations.Count == 0, "Billing events must carry ids, instants, dates, enums, booleans and decimals only: " + string.Join(", ", violations));
    }

    [Fact]
    public void EveryBillingEvent_ListedInTheHandoverExists()
    {
        string[] expected =
        [
            "BillingProfileChangedEvent", "DraftInvoiceCreatedEvent", "DraftInvoiceChangedEvent", "DraftInvoiceDiscardedEvent",
            "InvoiceIssuedEvent", "InvoiceStatusChangedEvent", "PaymentRecordedEvent", "PaymentApprovedEvent", "PaymentRejectedEvent",
            "PaymentAcceptedEvent", "PaymentAllocatedEvent", "PaymentAllocationReleasedEvent", "PaymentEarmarkReleasedEvent",
            "PaymentReversedEvent", "CreditNoteIssuedEvent", "RefundIssuedEvent", "CashUpClosedEvent", "CodRemittanceRecordedEvent",
            "BankStatementImportedEvent", "BankLineMatchedEvent", "BankLineUnmatchedEvent", "LoyaltyPointsAccruedEvent",
            "LoyaltyPointsRedeemedEvent", "AccountingExportRecordedEvent",
        ];

        var actual = typeof(InvoiceIssuedEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(InvoiceIssuedEvent).Namespace && typeof(Event).IsAssignableFrom(t))
            .Select(t => t.Name).ToHashSet();

        var missing = expected.Where(e => !actual.Contains(e)).ToList();
        Assert.True(missing.Count == 0, "Missing events: " + string.Join(", ", missing));
    }

    // ── U-X2 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_X2_EveryBillingAction_IsMapped_AndNoCustomerIsInAStaffList()
    {
        var actions = new[]
        {
            Actions.Billing.View, Actions.Billing.ManageCharges, Actions.Billing.DraftInvoices, Actions.Billing.IssueInvoices,
            Actions.Payments.Record, Actions.Payments.Allocate, Actions.Payments.Approve, Actions.Payments.Reverse,
            Actions.CreditNotes.Issue, Actions.Refunds.Issue, Actions.CashUp.Count, Actions.CashUp.Close,
            Actions.Reconciliation.Manage, Actions.AccountingExport.Run, Actions.BillingProfile.Manage,
        };

        foreach (var action in actions)
        {
            Assert.True(ActionRoles.TryGetRoles(action, out var roles), $"{action} has no ActionRoles row.");
            Assert.NotEmpty(roles);
            Assert.DoesNotContain("Customer", roles);
            Assert.All(roles, r => Assert.Contains(r, StaffRoles.All));
        }
    }

    [Theory]
    [InlineData("Payments.Approve", new[] { "Accountant", "Administrator" })]
    [InlineData("Payments.Reverse", new[] { "Accountant", "Administrator" })]
    [InlineData("CashUp.Close", new[] { "Accountant", "Administrator" })]
    [InlineData("BillingProfile.Manage", new[] { "Administrator" })]
    [InlineData("CreditNotes.Issue", new[] { "ShopManager", "Accountant", "Administrator" })]
    [InlineData("Refunds.Issue", new[] { "ShopManager", "Accountant", "Administrator" })]
    [InlineData("CashUp.Count", new[] { "Reception", "Accountant", "Administrator" })]
    public void TheRestrictedActions_AreRestrictedToTheRolesAppendixDAndTheOpenQuestionsName(string action, string[] expectedRoles)
    {
        Assert.True(ActionRoles.TryGetRoles(action, out var roles));
        Assert.Equal(expectedRoles.OrderBy(r => r), roles.OrderBy(r => r));
    }

    // ── U-X4 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_X4_NoCodeOutsidePaymentMethodRules_ReadsAPaymentMethodIdentity()
    {
        var root = RepositoryRoot();
        var members = Enumeration.GetAll<PaymentMethod>().Select(m => m.DisplayName).ToList();
        var pattern = new Regex(@"\bPaymentMethod\.(" + string.Join("|", members) + @")\b");

        var offenders = new List<string>();
        foreach (var area in new[] { "src/Application", "src/Read", "src/Api" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, area), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/obj/") || relative.Contains("/bin/")) continue;

                var text = File.ReadAllText(file);
                if (pattern.IsMatch(text)) offenders.Add(relative);
            }
        }

        Assert.True(offenders.Count == 0, "A payment method identity is read outside PaymentMethodRules (ADR-182): " + string.Join(", ", offenders));
    }

    // ── U-X6 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>ADR-012 / read-side-pattern section 4: the read side projects, it never rehydrates an aggregate. Two shapes give it away -- an
    /// aggregate fetched whole with <c>FirstOrDefaultAsync</c> straight after <c>AsNoTracking()</c>, and <c>ToDictionaryAsync</c> (which materialises
    /// the entity before its selectors run).</summary>
    [Fact]
    public void U_X6_TheBillingReadSide_NeverRehydratesAnAggregate()
    {
        var root = RepositoryRoot();
        var directory = Path.Combine(root, "src", "Read", "CatEcommerce.Read", "QueryHandlers", "Billing");
        var wholeEntity = new Regex(@"AsNoTracking\(\)\s*\.\s*(FirstOrDefaultAsync|SingleOrDefaultAsync|SingleAsync|FirstAsync)\s*\(");
        var dictionary = new Regex(@"\.ToDictionaryAsync\s*\(");

        var offenders = Directory.EnumerateFiles(directory, "*.cs")
            .Where(f => { var text = File.ReadAllText(f); return wholeEntity.IsMatch(text) || dictionary.IsMatch(text); })
            .Select(Path.GetFileName).ToList();

        Assert.True(offenders.Count == 0, "These Billing read handlers rehydrate an aggregate instead of projecting: " + string.Join(", ", offenders));
    }

    [Fact]
    public void ThereIsNoOnlinePaymentMethod_NoTokenColumn_NoGatewayType()
    {
        Assert.All(Enumeration.GetAll<PaymentMethod>(), m => Assert.False(m.IsOnline));

        var names = typeof(CatEcommerce.Domain.Aggregates.Billing.PaymentAggregate).Assembly.GetTypes().Where(t => t.Namespace is not null && t.Namespace.Contains("Billing")).Select(t => t.Name)
            .Concat(typeof(CatEcommerce.Domain.Aggregates.Billing.PaymentAggregate).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain(names, n => Regex.IsMatch(n, "Gateway|Webhook|PaymentIntent|CardNumber(?!Guard)|PaymentToken|SavedCard", RegexOptions.IgnoreCase));
    }

    // ── U-X5 ────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("billing.invoice-issued")]
    [InlineData("billing.payment-received")]
    [InlineData("billing.refund-issued")]
    public void U_X5_TheBillingTemplates_PassTemplateRules_AndAreTransactional(string key)
    {
        var definition = NotificationTemplateCatalogue.Find(key);
        Assert.NotNull(definition);
        Assert.Equal(NotificationCategory.Transactional, definition!.Category);
        Assert.Equal(NotificationUrgency.Normal, definition.Urgency);
        Assert.Equal(3, definition.Channels.Count);

        var contents = definition.Channels
            .Select(channel => definition.DefaultFor(channel))
            .Select(d => new TemplateContentInput(d!.Channel, d.Subject, d.Title, d.Body, d.LinkPath))
            .ToList();

        Assert.Null(TemplateRules.Validate(definition, contents));
    }

    [Fact]
    public void TheRefundTemplate_NeverCarriesTheReason()
    {
        var definition = NotificationTemplateCatalogue.Find(NotificationTemplateCatalogue.BillingRefundIssued)!;
        Assert.DoesNotContain(definition.Variables, v => v.Name.Contains("reason", StringComparison.OrdinalIgnoreCase));
    }

    // ── C-ALL-1 ─────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void EveryBillingTable_IsClassified_AsFinancialOrOperational_AndNoRecordTableAcceptsAHardDelete()
    {
        var billing = RecordClassification.All.Where(c => c.Schema == "billing").ToList();
        Assert.NotEmpty(billing);

        foreach (var table in billing)
        {
            if (table.Table == "billing_profile" || table.Table == "billing_profile_bank_account")
                Assert.Equal(RecordClass.Operational, table.RecordClass);
            else if (table.Table == "invoice_line")
                Assert.Equal(RecordClass.CurrentState, table.RecordClass);   // HO-XCT-001 ADR-298 amendment 2026-10-12: a Draft invoice lines are replaced; issued lines are immutable by trg_invoice_line_immutable
            else
                Assert.Equal(RecordClass.Financial, table.RecordClass);
        }

        // D-6: no Billing record is removed by a method -- only the transient working sets (and the replaceable Draft invoice lines, ADR-298 amendment 2026-10-12) are AggregateOnly.
        var aggregateOnly = billing.Where(c => c.HardDeletePolicy == HardDeletePolicy.AggregateOnly).Select(c => c.Table).OrderBy(t => t).ToList();
        Assert.Equal(["billing_profile_bank_account", "invoice_line", "payment_requested_allocation"], aggregateOnly);
    }

    [Fact]
    public void TheBillingViolationCodes_AreTheHandoversList_PlusTheRecordedAdditions()
    {
        string[] required =
        [
            "ChargeNotFound", "ChargeAlreadyRecorded", "ChargeAlreadyInvoiced", "ChargeNotActive", "ChargesMixedCustomerOrBranch",
            "TaxClassNotFound", "TaxRateNotConfigured", "InvalidQuantity", "DiscountExceedsAmount", "InvoiceNotFound", "InvoiceNotDraft",
            "InvoiceTooManyLines", "BillingProfileMissing", "FinancialYearConfigurationChanged", "AllocationExceedsOutstanding",
            "AllocationExceedsUnallocated", "EarmarkMismatch", "PaymentNotFound", "PaymentNotAccepted", "PaymentNotPending",
            "ApproverMustDiffer", "ReferenceRequired", "CollectorRequired", "ReferenceLooksLikeCardNumber", "ReceivedDateInFuture",
            "ReceivedDateTooOld", "PaymentHasRefunds", "ReasonRequired", "CreditExceedsInvoice", "CreditExceedsLine", "CreditNoteNotFound",
            "RefundMethodNotAllowed", "RefundExceedsAvailable", "CashUpClosed", "CashUpDateInFuture", "CountRequired",
            "VarianceExplanationRequired", "PendingApprovalsExist", "CollectionNotCod", "CollectionCourierMismatch",
            "RemittanceExceedsCollection", "StatementDuplicate", "StatementTooLarge", "StatementMalformed", "StatementLineNotFound",
            "BankLineAlreadyMatched", "PaymentAlreadyMatched", "MatchAmountMismatch", "MatchMethodNotTransfer", "ExportRangeTooLarge",
            "LoyaltyNotEnabled", "LoyaltyInsufficientPoints", "LoyaltyBelowMinimum", "LoyaltyRedemptionExceedsNet", "IdempotencyKeyConflict",
            "CurrencyMismatch",
        ];

        var declared = typeof(BusinessViolationCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => f.Name).ToHashSet();
        var absent = required.Where(r => !declared.Contains(r)).ToList();
        Assert.True(absent.Count == 0, "Missing violation codes: " + string.Join(", ", absent));
    }

    [Fact]
    public void TheNineBillingSettings_AreRegistered_WithTheHandoversDefaults()
    {
        var byKey = SettingDefinitions.All.ToDictionary(d => d.Key);

        Assert.Equal("7", byKey[SettingKeys.BillingFinancialYearStartMonth].Default);
        Assert.Equal("0", byKey[SettingKeys.BillingPaymentTermsDays].Default);
        Assert.Equal("50000.00", byKey[SettingKeys.BillingPaymentApprovalThreshold].Default);
        Assert.Equal("3", byKey[SettingKeys.BillingBankMatchDateToleranceDays].Default);
        Assert.Equal("7", byKey[SettingKeys.BillingCodRemittanceDueDays].Default);
        Assert.Equal("false", byKey[SettingKeys.BillingLoyaltyEnabled].Default);
        Assert.Equal("100.00", byKey[SettingKeys.BillingLoyaltySpendPerPoint].Default);
        Assert.Equal("1.00", byKey[SettingKeys.BillingLoyaltyPointValue].Default);
        Assert.Equal("100", byKey[SettingKeys.BillingLoyaltyMinimumRedemptionPoints].Default);

        Assert.True(byKey[SettingKeys.BillingPaymentTermsDays].BranchOverridable);
        Assert.True(byKey[SettingKeys.BillingPaymentApprovalThreshold].BranchOverridable);
        Assert.False(byKey[SettingKeys.BillingFinancialYearStartMonth].BranchOverridable);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CatEcommerce.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("CatEcommerce.sln was not found above the test binaries.");
    }
}

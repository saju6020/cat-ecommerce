using System.Reflection;
using System.Text.RegularExpressions;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Tests.Ordering;

/// <summary>
/// HO-ORD-001 -- the cross-cutting rules that fail the build rather than rely on review: the read side projects and never rehydrates an
/// aggregate (ADR-012, the rule every earlier backend broke), the action table is complete and role-correct (U-X2 of Billing's shape), no
/// event carries personal text (U-A8), no card or token anywhere (C-5), and the classification and violation-code duties of CONTRACTS.md.
/// </summary>
public sealed class OrderingConventionTests
{
    // ── ADR-012: the read side projects ─────────────────────────────────────────────────────────────
    private static readonly Regex Terminal = new(
        @"\.\s*(ToListAsync|FirstOrDefaultAsync|FirstAsync|SingleAsync|SingleOrDefaultAsync|ToDictionaryAsync|ToArrayAsync|ToHashSetAsync|LastAsync|LastOrDefaultAsync)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex Projection = new(@"\.\s*(Select|SelectMany)\s*[<(]", RegexOptions.Compiled);

    /// <summary>
    /// A statement is a rehydration when it starts from <c>AsNoTracking()</c> (or <c>Include</c>), ends in a materialiser, and passes through
    /// no <c>Select</c> / <c>SelectMany</c> on the way -- including <c>ToDictionaryAsync(k =&gt; …, v =&gt; …)</c> straight off a DbSet, which loads
    /// the whole entity before its selectors run. Broader than Billing's <c>U_X6</c> (backlog BIL 22): it flags a <c>Where(...).ToListAsync()</c>
    /// with no <c>Select</c> too.
    /// </summary>
    public static IReadOnlyList<string> FindRehydratingStatements(string source)
    {
        var offenders = new List<string>();
        var index = 0;
        while ((index = source.IndexOf("AsNoTracking()", index, StringComparison.Ordinal)) >= 0)
        {
            var start = source.LastIndexOf(';', index) + 1;
            var end = source.IndexOf(';', index);
            if (end < 0) end = source.Length;

            var statement = source[start..end];
            if (Terminal.IsMatch(statement) && !Projection.IsMatch(statement))
                offenders.Add(statement.Trim().ReplaceLineEndings(" ")[..Math.Min(160, statement.Trim().Length)]);

            index = end;
        }

        foreach (Match include in Regex.Matches(source, @"\.\s*Include\s*\("))
            offenders.Add("Include(…) at offset " + include.Index);

        return offenders;
    }

    [Fact]
    public void TheScanner_FlagsEveryRehydratingShape_AndPassesProjections()
    {
        Assert.NotEmpty(FindRehydratingStatements("var o = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);"));
        Assert.NotEmpty(FindRehydratingStatements("var o = await db.Orders.AsNoTracking().Where(o => o.X).ToListAsync();"));
        Assert.NotEmpty(FindRehydratingStatements("var d = await db.Orders.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.Number);"));
        Assert.NotEmpty(FindRehydratingStatements("var o = await db.Orders.AsNoTracking().Include(o => o.Lines).ToListAsync();"));

        Assert.Empty(FindRehydratingStatements("var o = await db.Orders.AsNoTracking().Select(o => new { o.Id }).ToListAsync();"));
        Assert.Empty(FindRehydratingStatements("var d = await db.Orders.AsNoTracking().Select(o => new { o.Id, o.Number }).ToDictionaryAsync(o => o.Id, o => o.Number);"));
        Assert.Empty(FindRehydratingStatements("var any = await db.Orders.AsNoTracking().AnyAsync(o => o.Id == id);"));
        Assert.Empty(FindRehydratingStatements("var q = db.Orders.AsNoTracking().Where(o => o.X);"));
        Assert.Empty(FindRehydratingStatements("var n = await db.Orders.AsNoTracking().SelectMany(o => o.Lines).Select(l => l.Id).ToListAsync();"));
    }

    [Fact]
    public void U_X6_TheOrderingReadSide_NeverRehydratesAnAggregate()
    {
        var root = RepositoryRoot();
        var directories = new[]
        {
            Path.Combine(root, "src", "Read", "CatEcommerce.Read", "QueryHandlers", "Order"),
            Path.Combine(root, "src", "Infrastructure", "CatEcommerce.Infrastructure", "Ordering"),
        };

        var offenders = new List<string>();
        foreach (var directory in directories.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                offenders.AddRange(FindRehydratingStatements(text).Select(s => $"{Path.GetFileName(file)}: {s}"));

                // A query handler never loads a root through the write-side repository either.
                if (file.Contains(Path.Combine("QueryHandlers", "Order"), StringComparison.Ordinal) && text.Contains("IAggregateRootRepository", StringComparison.Ordinal))
                    offenders.Add($"{Path.GetFileName(file)}: uses IAggregateRootRepository on the read side");
            }
        }

        Assert.True(offenders.Count == 0, "The Ordering read side must project (ADR-012):\n" + string.Join("\n", offenders));
    }

    // ── U-A8: events carry no personal text ─────────────────────────────────────────────────────────
    [Fact]
    public void U_A8_NoOrderingEventCarriesAStringProperty()
    {
        var eventTypes = typeof(OrderTransitionRules).Assembly.GetTypes()
            .Where(t => t.Namespace == "CatEcommerce.Domain.Events.Order" && typeof(Event).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        var violations = eventTypes
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        Assert.True(violations.Count == 0, "Ordering events carry ids, instants, enums, booleans and Money only: " + string.Join(", ", violations));
    }

    // ── authorisation ───────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void EveryOrderingAction_IsMapped_ToStaffRolesOnly()
    {
        string[] actions =
        [
            Actions.Orders.View, Actions.Orders.Fulfil, Actions.Orders.RecordDelivery, Actions.Orders.ConfirmPayment, Actions.Orders.ApprovePayment,
            Actions.Orders.Cancel, Actions.Orders.Reallocate, Actions.Orders.Reconcile, Actions.Orders.ManagePaymentSettings,
            Actions.Returns.Manage, Actions.Pos.Sell, Actions.DeliveryZones.View, Actions.DeliveryZones.Manage,
            Actions.Couriers.Manage, Actions.Discounts.View, Actions.Discounts.Manage,
        ];

        foreach (var action in actions)
        {
            Assert.True(ActionRoles.TryGetRoles(action, out var roles), $"{action} has no ActionRoles row.");
            Assert.NotEmpty(roles);
            Assert.DoesNotContain("Customer", roles);
            Assert.All(roles, r => Assert.Contains(r, StaffRoles.All));
        }
    }

    [Theory]
    [InlineData("Orders.View", new[] { "Reception", "Inventory", "ShopManager", "Accountant", "Administrator" })]
    [InlineData("Orders.Fulfil", new[] { "Reception", "ShopManager", "Administrator" })]
    [InlineData("Orders.RecordDelivery", new[] { "Reception", "ShopManager", "Administrator" })]
    [InlineData("Orders.ConfirmPayment", new[] { "Reception", "ShopManager", "Accountant", "Administrator" })]
    [InlineData("Orders.ApprovePayment", new[] { "Accountant", "Administrator" })] // = Billing's Payments.Approve (owner decision 4)
    [InlineData("Orders.Cancel", new[] { "Reception", "ShopManager", "Administrator" })]
    [InlineData("Orders.Reallocate", new[] { "Inventory", "ShopManager", "Administrator" })]
    [InlineData("Orders.Reconcile", new[] { "Accountant", "ShopManager", "Administrator" })]
    [InlineData("Returns.Manage", new[] { "Reception", "ShopManager", "Administrator" })]
    [InlineData("Pos.Sell", new[] { "Reception", "ShopManager", "Administrator" })]
    [InlineData("DeliveryZones.View", new[] { "ShopManager", "Accountant", "Administrator" })]
    [InlineData("DeliveryZones.Manage", new[] { "ShopManager", "Administrator" })]
    [InlineData("Couriers.Manage", new[] { "ShopManager", "Administrator" })]
    [InlineData("Discounts.View", new[] { "ShopManager", "Accountant", "Administrator" })]
    [InlineData("Discounts.Manage", new[] { "ShopManager", "Administrator" })]
    [InlineData("Orders.ManagePaymentSettings", new[] { "Administrator" })]
    [InlineData("Refunds.Issue", new[] { "ShopManager", "Accountant", "Administrator" })]
    public void TheRoleMapping_IsExactly_HandoverSection12_3(string action, string[] expectedRoles)
    {
        Assert.True(ActionRoles.TryGetRoles(action, out var roles));
        Assert.Equal(expectedRoles.OrderBy(r => r), roles.OrderBy(r => r));
    }

    [Fact]
    public void ANurse_HoldsNoOrderingAction()
    {
        // I-X4 at the table level: the nurse is none of the roles Appendix D gives orders.
        var ordering = ActionRoles.BuildTable(typeof(ActionRoles)).Where(a => a.Key.StartsWith("Orders.") || a.Key.StartsWith("Returns.") || a.Key.StartsWith("Pos.")
            || a.Key.StartsWith("DeliveryZones.") || a.Key.StartsWith("Couriers.") || a.Key.StartsWith("Discounts."));

        Assert.All(ordering, a => Assert.DoesNotContain("Nurse", a.Value));
        Assert.All(ordering, a => Assert.DoesNotContain("Vet", a.Value));
        Assert.All(ordering, a => Assert.DoesNotContain("Caretaker", a.Value));
    }

    // ── C-5: no card, wallet or token ───────────────────────────────────────────────────────────────
    [Fact]
    public void ThereIsNoGatewayCardWalletOrTokenType_OrMember_InTheOrderingDomain()
    {
        var pattern = new Regex(@"Gateway|PaymentIntent|CardNumber(?!Guard)|\bPan\b|Cvv|Cvc|Wallet|PaymentToken|SavedCard", RegexOptions.IgnoreCase);
        var assembly = typeof(OrderTransitionRules).Assembly;

        var names = assembly.GetTypes()
            .Where(t => t.Namespace is not null && (t.Namespace.Contains(".Order", StringComparison.Ordinal) || t.Name.Contains("Order") || t.Name.Contains("Cart") || t.Name.Contains("Checkout")))
            .SelectMany(t => new[] { t.Name }.Concat(t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Select(p => p.Name)))
            .ToList();

        Assert.DoesNotContain(names, n => pattern.IsMatch(n));
    }

    // ── enumerations ────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void TheEnumerationsOfSection10_1_Exist_WithTheHandoversMembers()
    {
        void Has<T>(params string[] members) where T : Enumeration =>
            Assert.Equal(members, Enumeration.GetAll<T>().OrderBy(e => e.Value).Select(e => e.DisplayName));

        Has<CartOwnerKind>("Guest", "Customer");
        Has<CartStatus>("Active", "Merged", "Abandoned");
        Has<OrderChannel>("Online", "Counter");
        Has<FulfilmentMethod>("Delivery", "Pickup", "Counter");
        Has<OrderPaymentMethod>("CashOnDelivery", "BankTransfer", "PayOnCollection", "InStore");
        Has<TenderMethod>("Cash", "InStoreCard");
        Has<DeliveryServiceLevel>("Standard", "Express");
        Has<OrderCancellationReason>("CustomerRequest", "StaffDecision", "PaymentWindowExpired");
        Has<DeliveryFailureReason>("Refused", "Undeliverable", "Uncollected");
        Has<ProductReturnClass>("Food", "Litter", "Other");
        Has<ReturnKind>("CustomerReturn", "DeliveryFailure");
        Has<ReturnStatus>("Requested", "Approved", "Rejected", "Received", "Closed");
        Has<ReturnReason>("WrongItem", "Damaged", "QualityDefect", "ChangedMind", "Recalled", "Other");
        Has<ItemCondition>("Sealed", "Opened", "Used");
        Has<DiscountKind>("Percentage", "FixedAmount");
        Has<DiscountScope>("Order", "Products", "Categories");
        Has<CheckoutChangeKind>("PriceIncreased", "PriceDecreased", "QuantityReduced", "NowUnavailable", "PrescriptionAuthorisationExpired", "PrescriptionCatRequired");
        Has<ReconciliationIssueKind>("TransferConfirmationFailed", "PaymentApprovalFailed", "DeliveryPaymentFailed", "CounterSaleFailed", "RefundFailed");
        Has<ReconciliationIssueStatus>("Open", "Resolved");
    }

    // ── settings ────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void TheTenOrdersSettings_AreRegistered_WithTheHandoversDefaultsBoundsAndOverrides()
    {
        var byKey = SettingDefinitions.All.ToDictionary(d => d.Key);

        void Check(string key, string defaultValue, string? min, string? max, bool branchOverridable)
        {
            var definition = byKey[key];
            Assert.Equal("Orders", definition.Group);
            Assert.Equal(defaultValue, definition.Default);
            Assert.Equal(min, definition.Min);
            Assert.Equal(max, definition.Max);
            Assert.Equal(branchOverridable, definition.BranchOverridable);
        }

        Check(SettingKeys.OrdersCodEnabled, "true", null, null, true);
        Check(SettingKeys.OrdersCodMaxOrderValue, "5000.00", "0.00", "1000000.00", true);
        Check(SettingKeys.OrdersBankTransferEnabled, "true", null, null, true);
        Check(SettingKeys.OrdersBankTransferWindowHours, "48", "1", "168", false);
        Check(SettingKeys.OrdersPayOnCollectionEnabled, "true", null, null, true);
        Check(SettingKeys.OrdersPickupCollectionDays, "7", "1", "60", true);
        Check(SettingKeys.OrdersReturnWindowDays, "7", "0", "90", false);
        Check(SettingKeys.OrdersMaxQuantityPerLine, "20", "1", "999", false);
        Check(SettingKeys.OrdersMaxCartLines, "50", "1", "50", false);
        Check(SettingKeys.OrdersGuestCartRetentionDays, "30", "1", "180", false);
        Assert.Equal(10, SettingDefinitions.All.Count(d => d.Group == "Orders"));
    }

    [Fact]
    public void TheOrderingViolationCodes_AreHandoverSection10_4()
    {
        string[] required =
        [
            "CartNotFound", "CartFull", "CartLineNotFound", "DuplicateCartLine", "SkuNotPurchasable", "SkuUnavailable", "QuantityOutOfRange",
            "PrescriptionCatRequired", "PrescriptionNotAuthorised", "CatNotOwnedByCaller", "CheckoutQuoteChanged", "CartEmpty", "NothingToPlace",
            "PolicyAcceptanceRequired", "DeliveryAddressNotFound", "AddressOutsideServedZones", "ServiceLevelUnavailable", "DeliverySlotUnavailable",
            "PickupBranchUnavailable", "PaymentMethodUnavailable", "RecipientPhoneRequired", "StockNoLongerAvailable", "DuplicateSubmission",
            "IdempotencyKeyRequired", "IdempotencyKeyReused", "RequestInProgress", "DiscountCodeNotFound", "DiscountCodeInactive",
            "DiscountCodeNotYetValid", "DiscountCodeExpired", "DiscountUsageLimitReached", "DiscountCustomerLimitReached", "DiscountFirstOrderOnly",
            "DiscountNotApplicableToItems", "DiscountMinimumSpendNotMet", "DiscountCodeAlreadyExists", "InvalidDiscountDefinition", "OrderNotFound",
            "InvalidOrderTransition", "CancellationNotAvailable", "CancellationReasonRequired", "OrderNotAwaitingPayment", "OrderNotExpirable",
            "TransferAmountMismatch", "PaymentAlreadyAwaitingApproval", "NoPaymentAwaitingApproval", "SecondApproverMustDiffer", "OrderNotFullyAllocated",
            "BatchRecalled", "BatchExpired", "CourierNotFound", "CodCollectionRequired", "CollectionPaymentMismatch", "TerminalReferenceRequired",
            "DeliveryFailureReasonInvalid", "TendersDoNotCoverTotal", "ReallocationReasonRequired", "ReturnNotFound", "ReturnWindowClosed",
            "ReturnQuantityExceeded", "ReturnNotEligibleOpenedFood", "ReturnNotEligibleUsedLitter", "DefectConfirmationRequired", "InvalidReturnTransition",
            "ReturnReceiptExceedsApproved", "RefundExceedsAmountPaid", "RefundMethodNotAllowed", "RefundMethodUnavailable",
            "RefundReferenceRequired", "DeliveryZoneNotFound", "DeliveryZoneNameAlreadyExists", "InvalidDeliveryZone", "CourierNameAlreadyExists",
            "InvalidBankTransferAccount", "ReconciliationIssueNotFound",
        ];

        var declared = typeof(BusinessViolationCodes).GetFields(BindingFlags.Public | BindingFlags.Static).ToDictionary(f => f.Name, f => (string?)f.GetValue(null));
        var absent = required.Where(r => !declared.ContainsKey(r)).ToList();
        Assert.True(absent.Count == 0, "Missing violation codes: " + string.Join(", ", absent));
        Assert.All(required, r => Assert.Equal(r, declared[r]));
    }

    // ── C-ALL-1: classification ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void EveryOrderingTable_IsClassified_AndNoMoneyOrOrderRecordAcceptsAHardDelete()
    {
        var ordering = RecordClassification.All.Where(c => c.Schema is "ordering" or "ops").ToList();
        Assert.NotEmpty(ordering);

        foreach (var table in ordering)
        {
            if (table.Schema == "ops") continue;

            // Orders, their lines and payments are Financial (money owed, paid or refunded); configuration is Operational. None is Purgeable.
            Assert.NotEqual(HardDeletePolicy.Purgeable, table.HardDeletePolicy);
        }

        Assert.Contains(ordering, c => c is { Schema: "ops", Table: "idempotency_record" });
        Assert.Contains(ordering, c => c is { Schema: "ops", Table: "inbound_webhook_receipt" });
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CatEcommerce.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("CatEcommerce.sln was not found above the test binaries.");
    }
}

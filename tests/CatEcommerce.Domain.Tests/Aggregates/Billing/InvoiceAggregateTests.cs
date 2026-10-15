using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Billing;

/// <summary>HO-BIL-001 §15 group C (U-C5…U-C8) -- an issued invoice is immutable in the aggregate (US-BIL-05 AC1),
/// issue freezes tax and totals (US-BIL-07), and the derived status follows the postings.</summary>
public sealed class InvoiceAggregateTests
{
    private static string ErrorOf(Action act) => Assert.Throws<DomainRuleViolationException>(act).ErrorCode;

    [Fact]
    public void CreateDraft_ItemisesTheLines_AndRaisesTheDraftEvent()
    {
        var invoice = BillingTestData.Draft(BillingTestData.Line(2m, 450m, 50m), BillingTestData.Line(1m, 100m));

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(2, invoice.Lines.Count);
        Assert.Null(invoice.Number);
        Assert.Contains(invoice.Events, e => e is DraftInvoiceCreatedEvent);
    }

    [Fact]
    public void Issue_FreezesTheRateTaxAndTotals_AndNumbersTheInvoice()
    {
        var taxClass = Guid.NewGuid();
        var invoice = BillingTestData.Draft(BillingTestData.Line(2m, 450m, 50m, taxClass));
        invoice.ClearEvents();

        invoice.Issue(BillingTestData.IssueDto(invoice, 15m, "INV-MAIN-2627-000042", 42, termsDays: 7));

        var line = Assert.Single(invoice.Lines);
        Assert.Equal(15m, line.TaxRatePercent);
        Assert.Equal("STD", line.TaxClassCode);
        Assert.Equal(850m, line.NetAmount);
        Assert.Equal(127.50m, line.TaxAmount);
        Assert.Equal(977.50m, line.GrossAmount);
        Assert.True(line.IsFrozen);
        Assert.Equal(977.50m, invoice.Total);
        Assert.Equal(50m, invoice.DiscountTotal);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Equal("INV-MAIN-2627-000042", invoice.Number);
        Assert.Equal(42, invoice.SequenceNumber);
        Assert.Equal(invoice.IssueDate!.Value.AddDays(7), invoice.DueOn);
        Assert.NotNull(invoice.Seller);
        Assert.NotNull(invoice.BillTo);
        Assert.Contains(invoice.Events, e => e is InvoiceIssuedEvent { Total: 977.50m });
    }

    // ── U-C5 ────────────────────────────────────────────────────────────────────────────────────────
    public static TheoryData<string> NonDraftStatuses => new() { "Issued", "PartPaid", "Paid", "Credited", "Discarded" };

    private static InvoiceAggregate InStatus(string status)
    {
        if (status == "Discarded")
        {
            var draft = BillingTestData.Draft();
            draft.Discard(BillingTestData.Actor());
            return draft;
        }

        var invoice = BillingTestData.Issued(1000m);
        var actor = BillingTestData.Actor();
        switch (status)
        {
            case "PartPaid": invoice.ApplyPayment(400m, actor); break;
            case "Paid": invoice.ApplyPayment(1000m, actor); break;
            case "Credited": invoice.ApplyCredit(1000m, actor); break;
        }

        Assert.Equal(status, invoice.Status.DisplayName);
        return invoice;
    }

    [Theory]
    [MemberData(nameof(NonDraftStatuses))]
    public void U_C5_EveryContentMutation_OfANonDraftInvoice_IsRefused(string status)
    {
        var invoice = InStatus(status);
        var actor = BillingTestData.Actor();
        var lines = new[] { BillingTestData.Line() };

        Assert.Equal(BusinessViolationCodes.InvoiceNotDraft, ErrorOf(() => invoice.SetDraftLines(lines, actor)));
        Assert.Equal(BusinessViolationCodes.InvoiceNotDraft, ErrorOf(() => invoice.ReviseCorrectedLines(lines, actor)));
        Assert.Equal(BusinessViolationCodes.InvoiceNotDraft, ErrorOf(() => invoice.Discard(actor)));
        Assert.Equal(BusinessViolationCodes.InvoiceNotDraft, ErrorOf(() => invoice.RemoveLoyaltyRedemption(actor)));
        Assert.Equal(BusinessViolationCodes.InvoiceNotDraft, ErrorOf(() => invoice.ApplyLoyaltyRedemption(new LoyaltyRedemptionDto(100, 100m, Guid.NewGuid(), 500m), actor)));
        Assert.Equal(BusinessViolationCodes.InvoiceNotDraft, ErrorOf(() => invoice.Issue(BillingTestData.IssueDto(new Dictionary<Guid, TaxRateInfo>()))));
    }

    [Fact]
    public void AnIssuedInvoice_ExposesNoMutableLineOrContentSetter()
    {
        var setters = typeof(InvoiceAggregate).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToList();
        Assert.Empty(setters);

        Assert.DoesNotContain(typeof(CatEcommerce.Domain.Entities.InvoiceLine).GetProperties(), p => p.SetMethod is { IsPublic: true });
    }

    // ── U-C6, U-C7, U-C8 ────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_C6_ALineWhoseClassHasNoRateAtIssue_RefusesNamingTheClass()
    {
        var taxClass = Guid.NewGuid();
        var invoice = BillingTestData.Draft(BillingTestData.Line(taxClassId: taxClass));
        var rates = new Dictionary<Guid, TaxRateInfo> { [taxClass] = new(taxClass, "REDUCED-5", null) };

        var ex = Assert.Throws<DomainRuleViolationException>(() => invoice.Issue(BillingTestData.IssueDto(rates)));

        Assert.Equal(BusinessViolationCodes.TaxRateNotConfigured, ex.ErrorCode);
        Assert.Contains("REDUCED-5", ex.Message);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
    }

    [Fact]
    public void U_C7_IssueWithoutABillingProfile_IsRefused()
    {
        var invoice = BillingTestData.Draft();
        var dto = BillingTestData.IssueDto(invoice) with { Seller = null! };

        Assert.Equal(BusinessViolationCodes.BillingProfileMissing, ErrorOf(() => invoice.Issue(dto)));
    }

    [Fact]
    public void U_C8_FiveHundredAndOneLines_AreRefused()
    {
        var lines = Enumerable.Range(0, BillingRules.MaxInvoiceLines + 1).Select(_ => BillingTestData.Line()).ToArray();
        Assert.Equal(BusinessViolationCodes.InvoiceTooManyLines, ErrorOf(() => BillingTestData.Draft(lines)));

        var five = Enumerable.Range(0, BillingRules.MaxInvoiceLines).Select(_ => BillingTestData.Line()).ToArray();
        Assert.Equal(BillingRules.MaxInvoiceLines, BillingTestData.Draft(five).Lines.Count);
    }

    [Fact]
    public void ADraftNeedsAtLeastOneLine()
    {
        var invoice = new InvoiceAggregate(Guid.NewGuid());
        var dto = new DraftInvoiceDto(Guid.NewGuid(), Guid.NewGuid(), "BDT", [], null, null, null, DateTimeOffset.UtcNow, BillingTestData.Actor());
        Assert.Equal(BusinessViolationCodes.InvoiceHasNoLines, ErrorOf(() => invoice.CreateDraft(dto)));
    }

    // ── Postings ────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void ApplyPayment_PartThenWhole_DerivesPartPaidThenPaid_AndRaisesTheStatusEventOnlyOnChange()
    {
        var invoice = BillingTestData.Issued(1000m);
        invoice.ClearEvents();
        var actor = BillingTestData.Actor();

        invoice.ApplyPayment(400m, actor);
        Assert.Equal(InvoiceStatus.PartPaid, invoice.Status);
        Assert.Equal(600m, invoice.Outstanding);

        invoice.ApplyPayment(100m, actor);
        Assert.Equal(InvoiceStatus.PartPaid, invoice.Status);

        invoice.ApplyPayment(500m, actor);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);

        var changes = invoice.Events.OfType<InvoiceStatusChangedEvent>().ToList();
        Assert.Equal(2, changes.Count);
        Assert.Equal((InvoiceStatus.Issued, InvoiceStatus.PartPaid), (changes[0].From, changes[0].To));
        Assert.Equal((InvoiceStatus.PartPaid, InvoiceStatus.Paid), (changes[1].From, changes[1].To));
    }

    [Fact]
    public void ApplyPayment_AboveOutstanding_NamesTheAvailableAmount()
    {
        var invoice = BillingTestData.Issued(1000m);
        invoice.ApplyPayment(600m, BillingTestData.Actor());

        var ex = Assert.Throws<DomainRuleViolationException>(() => invoice.ApplyPayment(400.01m, BillingTestData.Actor()));

        Assert.Equal(BusinessViolationCodes.AllocationExceedsOutstanding, ex.ErrorCode);
        Assert.Contains("BDT 400.00", ex.Message);
        Assert.Contains("INV-MAIN-2627-000001", ex.Message);
    }

    [Fact]
    public void ReleasePayment_ReturnsTheInvoiceToPartPaid()
    {
        var invoice = BillingTestData.Issued(1000m);
        var actor = BillingTestData.Actor();
        invoice.ApplyPayment(1000m, actor);

        invoice.ReleasePayment(300m, actor);

        Assert.Equal(InvoiceStatus.PartPaid, invoice.Status);
        Assert.Equal(300m, invoice.Outstanding);
        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => invoice.ReleasePayment(701m, actor)));
    }

    [Fact]
    public void ApplyCredit_ReducesOutstanding_AndAFullCreditMakesItCredited()
    {
        var invoice = BillingTestData.Issued(1000m);
        var actor = BillingTestData.Actor();

        invoice.ApplyCredit(300m, actor);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Equal(700m, invoice.Outstanding);

        Assert.Equal(BusinessViolationCodes.CreditExceedsInvoice, ErrorOf(() => invoice.ApplyCredit(700.01m, actor)));

        invoice.ApplyCredit(700m, actor);
        Assert.Equal(InvoiceStatus.Credited, invoice.Status);
    }

    [Fact]
    public void ApplyCredit_WhileTheCreditedPartIsStillPaid_RequiresTheReleaseFirst()
    {
        var invoice = BillingTestData.Issued(1000m);
        var actor = BillingTestData.Actor();
        invoice.ApplyPayment(1000m, actor);

        Assert.Equal(BusinessViolationCodes.CreditExceedsInvoice, ErrorOf(() => invoice.ApplyCredit(500m, actor)));

        invoice.ReleasePayment(500m, actor);
        invoice.ApplyCredit(500m, actor);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status); // outstanding 0, paid 500 > 0
    }

    [Fact]
    public void Postings_AreRefusedOnADraft()
    {
        var draft = BillingTestData.Draft();
        var actor = BillingTestData.Actor();
        Assert.Equal(BusinessViolationCodes.InvoiceNotIssued, ErrorOf(() => draft.ApplyPayment(1m, actor)));
        Assert.Equal(BusinessViolationCodes.InvoiceNotIssued, ErrorOf(() => draft.ApplyCredit(1m, actor)));
    }

    [Fact]
    public void Discard_MarksEveryLine_SoTheChargesLeaveTheUniqueIndexScope()
    {
        var draft = BillingTestData.Draft(BillingTestData.Line(), BillingTestData.Line());

        draft.Discard(BillingTestData.Actor());

        Assert.Equal(InvoiceStatus.Discarded, draft.Status);
        Assert.All(draft.Lines, l => Assert.True(l.InvoiceDiscarded));
    }

    [Fact]
    public void SetDraftLines_KeepsTheLinesWhoseChargeStays_AndReplacesTheRest()
    {
        var keep = Guid.NewGuid();
        var drop = Guid.NewGuid();
        var add = Guid.NewGuid();
        var draft = BillingTestData.Draft(BillingTestData.Line(chargeId: keep), BillingTestData.Line(chargeId: drop));
        var keptLineId = draft.Lines.Single(l => l.ChargeId == keep).Id;

        draft.SetDraftLines([BillingTestData.Line(chargeId: add), BillingTestData.Line(chargeId: keep)], BillingTestData.Actor());

        Assert.Equal(2, draft.Lines.Count);
        Assert.Equal(keptLineId, draft.Lines.Single(l => l.ChargeId == keep).Id);
        Assert.DoesNotContain(draft.Lines, l => l.ChargeId == drop);
        Assert.Contains(draft.Lines, l => l.ChargeId == add);
    }

    [Fact]
    public void Renumber_IsRefusedOnceAnythingIsPosted()
    {
        var invoice = BillingTestData.Issued(1000m);
        invoice.Renumber("INV-MAIN-2627-000009", 9);
        Assert.Equal(9, invoice.SequenceNumber);

        invoice.ApplyPayment(10m, BillingTestData.Actor());
        Assert.Equal(BusinessViolationCodes.InvoiceNotDraft, ErrorOf(() => invoice.Renumber("INV-MAIN-2627-000010", 10)));
    }

    [Fact]
    public void ReassignOwner_MovesADraftButNeverAnIssuedInvoice()
    {
        var draft = BillingTestData.Draft();
        var survivor = Guid.NewGuid();
        draft.ReassignOwner(survivor);
        Assert.Equal(survivor, draft.CustomerId);

        var issued = BillingTestData.Issued();
        var original = issued.CustomerId;
        issued.ReassignOwner(survivor);
        Assert.Equal(original, issued.CustomerId);
    }

    [Fact]
    public void Loyalty_RedemptionAboveTheQualifyingNet_IsRefused_AndAFeeDoesNotQualify()
    {
        var draft = BillingTestData.Draft(BillingTestData.Line(1m, 300m), BillingTestData.Line(1m, 500m, kind: ItemKind.Fee));
        var actor = BillingTestData.Actor();

        Assert.Equal(BusinessViolationCodes.LoyaltyRedemptionExceedsNet,
            ErrorOf(() => draft.ApplyLoyaltyRedemption(new LoyaltyRedemptionDto(400, 400m, Guid.NewGuid(), 300m), actor)));

        draft.ApplyLoyaltyRedemption(new LoyaltyRedemptionDto(100, 100m, Guid.NewGuid(), 300m), actor);
        Assert.NotNull(draft.LoyaltyRedemption);

        draft.Issue(BillingTestData.IssueDto(draft, 10m));
        var feeLine = draft.Lines.Single(l => l.ItemKind == ItemKind.Fee);
        Assert.Equal(0m, feeLine.InvoiceDiscountShare);
        Assert.Equal(100m, draft.Lines.Sum(l => l.InvoiceDiscountShare));
        Assert.Equal(770m, draft.Total); // 200 + 20 tax, and the untouched fee 500 + 50 tax
    }
}

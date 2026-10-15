using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Billing;

/// <summary>HO-BIL-001 C-5 / section 19 gate 2 -- <b>every</b> free-text field of a money record refuses a card number, so one never reaches a
/// Forbidden-to-delete row or the audit snapshot copied from it. One case per field; each also proves nothing was stored.</summary>
public sealed class CardNumberFieldTests
{
    private const string Pan = "card 4111 1111 1111 1111 charged twice";
    private const string BengaliPan = "৪১১১ ১১১১ ১১১১ ১১১১";

    private static void AssertRefused(Action act)
    {
        var error = Assert.Throws<DomainRuleViolationException>(act);
        Assert.Equal(BusinessViolationCodes.ReferenceLooksLikeCardNumber, error.ErrorCode);
        Assert.DoesNotContain("4111", error.Message); // the refused value is never echoed
    }

    [Theory]
    [InlineData(Pan)]
    [InlineData(BengaliPan)]
    public void ARejectionReason_IsRefused(string reason)
    {
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(60_000m, PaymentMethod.BankTransfer, "TRX-1"));

        AssertRefused(() => payment.Reject(Guid.NewGuid(), reason, DateTimeOffset.UtcNow, BillingTestData.Actor()));

        Assert.Equal(PaymentStatus.PendingApproval, payment.Status);
        Assert.Null(payment.RejectionReason);
    }

    [Theory]
    [InlineData(Pan)]
    [InlineData(BengaliPan)]
    public void AReversalReason_IsRefused(string reason)
    {
        var payment = BillingTestData.Payment();

        AssertRefused(() => payment.Reverse(reason, DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), BillingTestData.Actor()));

        Assert.Equal(PaymentStatus.Accepted, payment.Status);
        Assert.Null(payment.ReversalReason);
    }

    [Fact]
    public void AnEarmarkReleaseReason_IsRefused()
    {
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(earmark: new PaymentEarmark(EarmarkKind.BoardingStay, Guid.NewGuid(), "Stay deposit")));

        AssertRefused(() => payment.ReleaseEarmark(Pan, DateTimeOffset.UtcNow, BillingTestData.Actor()));

        Assert.False(payment.Earmark!.IsReleased);
        Assert.Null(payment.Earmark.ReleaseReason);
    }

    [Fact]
    public void AnEarmarkLabel_IsRefused()
    {
        AssertRefused(() => BillingTestData.Payment(BillingTestData.PaymentDto(earmark: new PaymentEarmark(EarmarkKind.Order, Guid.NewGuid(), Pan))));
    }

    [Fact]
    public void ACreditNoteReason_IsRefused()
    {
        var note = new CreditNoteAggregate(Guid.NewGuid());
        var line = new CreditNoteLineInput(Guid.NewGuid(), "Item", null, 10m, 10m, 0m, 0m, 10m);

        AssertRefused(() => note.Issue(new CreditNoteIssueDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "BDT", "CN-1", "2026-27", 1, CreditNoteKind.Return, Pan, [line], 0m,
            DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), null, BillingTestData.Actor())));

        Assert.Empty(note.Lines);
    }

    [Fact]
    public void ACashUpExplanation_IsRefused()
    {
        var cashUp = new CashUpAggregate(Guid.NewGuid());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        cashUp.Open(Guid.NewGuid(), today, BillingTestData.Actor().UserContext);

        AssertRefused(() => cashUp.RecordCount(10m, Pan, today, DateTimeOffset.UtcNow, BillingTestData.Actor()));

        Assert.Null(cashUp.CountedCash);
        Assert.Null(cashUp.Explanation);
    }

    [Fact]
    public void ABankMatchUnmatchReason_IsRefused()
    {
        var match = new BankMatchAggregate(Guid.NewGuid());
        match.Match(new BankMatchDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BankMatchMethod.Manual, DateTimeOffset.UtcNow, BillingTestData.Actor()));

        AssertRefused(() => match.Unmatch(Pan, DateTimeOffset.UtcNow, BillingTestData.Actor()));

        Assert.True(match.IsActive);
        Assert.Null(match.UnmatchReason);
    }

    private static ChargeRequestDto ChargeRequest(string description) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BusinessLine.Clinic, ItemKind.Service, ChargeSourceKind.Appointment, Guid.NewGuid(), "service", null, null,
            description, 1m, new Money(100m, "BDT"), new Money(0m, "BDT"), Guid.NewGuid(), new DateOnly(2026, 10, 7));

    private static ChargeAggregate NewCharge()
    {
        var charge = new ChargeAggregate(Guid.NewGuid());
        charge.Record(ChargeRequest("Consultation"), new ChargeRecordContext("BDT", DateTimeOffset.UtcNow, BillingTestData.Actor()));
        return charge;
    }

    [Fact]
    public void AChargeVoidReason_IsRefused()
    {
        var charge = NewCharge();

        AssertRefused(() => charge.Void(Pan, DateTimeOffset.UtcNow, BillingTestData.Actor()));

        Assert.Equal(ChargeStatus.Active, charge.Status);
        Assert.Null(charge.VoidReason);
    }

    [Fact]
    public void AManualChargeDescription_IsRefused()
    {
        var charge = new ChargeAggregate(Guid.NewGuid());

        AssertRefused(() => charge.Record(ChargeRequest(Pan), new ChargeRecordContext("BDT", DateTimeOffset.UtcNow, BillingTestData.Actor())));
    }

    [Fact]
    public void AnInvoiceNote_IsRefused()
    {
        var invoice = new InvoiceAggregate(Guid.NewGuid());

        AssertRefused(() => invoice.CreateDraft(new DraftInvoiceDto(
            Guid.NewGuid(), Guid.NewGuid(), "BDT", [BillingTestData.Line()], Pan, null, null, DateTimeOffset.UtcNow, BillingTestData.Actor())));

        Assert.Null(invoice.Notes);
    }

    [Fact]
    public void ACorrectedDraftLineDescription_IsRefused()
    {
        var draft = new InvoiceAggregate(Guid.NewGuid());
        draft.CreateDraft(new DraftInvoiceDto(
            Guid.NewGuid(), Guid.NewGuid(), "BDT", [BillingTestData.Line(description: "Original")], null, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, BillingTestData.Actor()));

        AssertRefused(() => draft.ReviseCorrectedLines([BillingTestData.Line(description: Pan)], BillingTestData.Actor()));

        Assert.Equal("Original", Assert.Single(draft.Lines).Description);
    }

    // ── F-3: the remaining free-text fields ─────────────────────────────────────────────────────────
    private static BillingProfileSetDto Profile(
        string legalName = "PurrCare Ltd", string address = "1 Road", string? phone = "01700000000", string? email = "a@b.test", string? footer = "Thanks",
        string bankName = "Bank", string accountName = "PurrCare Ltd", string? branchName = "Gulshan", string accountNumber = "4111111111111111", string tax = "4111111111111111") =>
        new(legalName, tax, [address], phone, email, footer, [new BankAccountInput(bankName, accountName, accountNumber, branchName, "012345678")], BillingTestData.Actor());

    [Theory]
    [InlineData("legalName")]
    [InlineData("address")]
    [InlineData("footer")]
    [InlineData("phone")]
    [InlineData("email")]
    [InlineData("bankName")]
    [InlineData("accountName")]
    [InlineData("branchName")]
    public void ABillingProfileTextField_RefusesACardNumber(string field)
    {
        var profile = new BillingProfileAggregate(Guid.NewGuid());
        var dto = field switch
        {
            "legalName" => Profile(legalName: Pan),
            "address" => Profile(address: Pan),
            "footer" => Profile(footer: Pan),
            "phone" => Profile(phone: "4111 1111 1111 1111"),
            "email" => Profile(email: "4111-1111-1111-1111@x.test"),
            "bankName" => Profile(bankName: Pan),
            "accountName" => Profile(accountName: Pan),
            _ => Profile(branchName: Pan),
        };

        AssertRefused(() => profile.Set(dto));

        Assert.Empty(profile.BankAccounts);
        Assert.Empty(profile.AddressLines);
    }

    [Fact]
    public void ABillingProfile_MayCarryALongNumericAccountNumberAndRegistrationNumber_ThoseAreIdentifiersNotFreeText()
    {
        var profile = new BillingProfileAggregate(Guid.NewGuid());

        profile.Set(Profile());

        Assert.Equal("4111111111111111", Assert.Single(profile.BankAccounts).AccountNumber);
    }

    [Fact]
    public void AChargeReportingTag_IsRefused()
    {
        var request = ChargeRequest("Consultation") with { ReportingTag = "tag 4111 1111 1111 1111" };

        AssertRefused(() => new ChargeAggregate(Guid.NewGuid()).Record(request, new ChargeRecordContext("BDT", DateTimeOffset.UtcNow, BillingTestData.Actor())));
    }

    [Fact]
    public void ABankStatementAccountLabel_IsRefused_AndItsFileNameIsMasked()
    {
        var statement = new BankStatementAggregate(Guid.NewGuid());
        var line = new BankStatementLineInput(2, DateOnly.FromDateTime(DateTime.UtcNow), 10m, BankEntryDirection.Credit, "R", "d", null);
        BankStatementImportDto Import(string label, string file) => new(
            label, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), DateOnly.FromDateTime(DateTime.UtcNow), file, new string('a', 64), [line], DateTimeOffset.UtcNow, BillingTestData.Actor());

        AssertRefused(() => statement.Import(Guid.NewGuid(), Import("Main " + Pan, "s.csv")));

        statement.Import(Guid.NewGuid(), Import("Main", "4111111111111111.csv"));
        Assert.Equal("••••1111.csv", statement.SourceFileName);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("name")]
    public void ACourierCodeOrName_IsRefused(string field)
    {
        var collector = field == "code"
            ? new PaymentCollector(CollectorKind.Courier, null, "5555555555554444", "Pathao")
            : new PaymentCollector(CollectorKind.Courier, null, "PATHAO", "Pathao 4111 1111 1111 1111");

        AssertRefused(() => BillingTestData.Payment(BillingTestData.PaymentDto(method: PaymentMethod.CashOnDelivery, collector: collector)));
    }

    [Fact]
    public void AnOrdinaryReasonWithNumbersInIt_IsStillAccepted()
    {
        var payment = BillingTestData.Payment();

        payment.Reverse("Entered twice, receipt R-000123 and slip 004512 on 2026-10-07", DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), BillingTestData.Actor());

        Assert.Equal(PaymentStatus.Reversed, payment.Status);
    }

    [Fact]
    public void ABankStatementLine_IsMaskedBeforeItIsTruncated_AndItsTransactionIdToo()
    {
        var statement = new BankStatementAggregate(Guid.NewGuid());
        var padding = new string('x', 95);
        var line = new BankStatementLineInput(
            2, DateOnly.FromDateTime(DateTime.UtcNow), 10m, BankEntryDirection.Credit,
            padding + " 4111 1111 1111 1111", new string('y', 490) + " 4111 1111 1111 1111", "ID 4111 1111 1111 1111");

        statement.Import(Guid.NewGuid(), new BankStatementImportDto(
            "Main", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), DateOnly.FromDateTime(DateTime.UtcNow), "s.csv", new string('a', 64), [line], DateTimeOffset.UtcNow, BillingTestData.Actor()));

        var stored = Assert.Single(statement.Lines);
        foreach (var text in new[] { stored.Reference!, stored.Description!, stored.BankTransactionId! })
        {
            Assert.DoesNotContain("4111", text);
            Assert.False(CatEcommerce.Shared.Common.Helpers.CardNumberGuard.LooksLikeCardNumber(text));
        }

        Assert.Contains("••••1111", stored.BankTransactionId);
    }
}

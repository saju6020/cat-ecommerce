using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Tests.Ordering;

/// <summary>HO-ORD-001 §8.16 (ADR-205), C-5 -- the reconciliation issue keeps what staff entered, never a card number and never an exception message; an
/// accountant resolves it once; and a card-like text is never accepted as a payment reference.</summary>
public sealed class ReconciliationIssueTests
{
    private static readonly UserContext Staff = new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Reception"] };
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid BranchId = Guid.NewGuid();

    private static OrderReconciliationIssueAggregate Raised(string? reference = "BANK-REF-1")
    {
        var issue = new OrderReconciliationIssueAggregate(Guid.NewGuid());
        issue.Raise(new ReconciliationIssueDto(
            issue.Id, Guid.NewGuid(), ReconciliationIssueKind.TransferConfirmationFailed, new EnteredPaymentDetails("BankTransfer", 1927.50m, reference, T0), "bdt",
            "InvalidOperationException: the payment could not be recorded and was rolled back.", Guid.NewGuid(), Staff.UserId, T0, Staff, BranchId));
        return issue;
    }

    [Fact]
    public void ARaisedIssue_KeepsWhatWasEntered_IsOpen_AndRaisesAnEventWithoutText()
    {
        var issue = Raised();

        Assert.Equal(ReconciliationIssueStatus.Open, issue.Status);
        Assert.Equal(1927.50m, issue.Amount);
        Assert.Equal("BDT", issue.CurrencyCode);
        Assert.Equal("BANK-REF-1", issue.PaymentReference);
        Assert.Equal(Staff.UserId, issue.RaisedBy);
        Assert.Equal(BranchId, issue.BranchId);
        var raised = Assert.Single(issue.Events.OfType<CatEcommerce.Domain.Events.Order.ReconciliationIssueRaisedEvent>());
        Assert.Equal(issue.Id, raised.IssueId);
    }

    [Fact]
    public void ACardLikeReference_IsMaskedBeforeItIsKept()
    {
        var issue = Raised("4111 1111 1111 1111");

        Assert.DoesNotContain("4111 1111 1111", issue.PaymentReference);
        Assert.EndsWith("1111", issue.PaymentReference);
    }

    [Fact]
    public void AnIssue_IsResolvedOnce_WithANote()
    {
        var issue = Raised();
        var accountant = new UserContext(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Accountant"] };

        var noNote = Assert.Throws<DomainRuleViolationException>(() => issue.Resolve(accountant.UserId, " ", T0, accountant, Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.ReconciliationNoteRequired, noNote.ErrorCode);

        issue.Resolve(accountant.UserId, "Entered by hand", T0.AddHours(1), accountant, Guid.NewGuid());
        Assert.Equal(ReconciliationIssueStatus.Resolved, issue.Status);
        Assert.Equal(accountant.UserId, issue.ResolvedBy);
        Assert.Equal("Entered by hand", issue.ResolutionNote);

        var twice = Assert.Throws<DomainRuleViolationException>(() => issue.Resolve(accountant.UserId, "again", T0.AddHours(2), accountant, Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.ReconciliationIssueAlreadyResolved, twice.ErrorCode);
    }

    [Theory]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("4111-1111-1111-1111")]
    [InlineData("5555555555554444")]
    public void OrderFreeText_RefusesACardNumber_WithoutEchoingIt(string text)
    {
        var refusal = Assert.Throws<DomainRuleViolationException>(() => OrderFreeText.RefuseCardNumber(text, "reference"));

        Assert.Equal(BusinessViolationCodes.ReferenceLooksLikeCardNumber, refusal.ErrorCode);
        Assert.DoesNotContain(text, refusal.Message);
    }

    [Theory]
    [InlineData("BANK-REF-12345")]
    [InlineData("TXN 998877")]
    [InlineData("ORD-2026-000123")]
    public void OrderFreeText_AcceptsAnOrdinaryReference(string text) => OrderFreeText.RefuseCardNumber(text, "reference");
}

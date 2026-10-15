using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Billing;

/// <summary>HO-BIL-001 section 15 group G (U-G1..U-G3, U-G5, U-G6) -- reconciling COD remittances and bank statements (US-BIL-06; ADR-189).</summary>
public sealed class ReconciliationTests
{
    private static readonly DateOnly Day = new(2026, 10, 7);
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static string ErrorOf(Action act) => Assert.Throws<DomainRuleViolationException>(act).ErrorCode;

    private static BankLineCandidateInput Line(decimal amount, int dayOffset = 0, string? reference = "TRX-0012", string? description = null) =>
        new(Guid.NewGuid(), Day.AddDays(dayOffset), amount, reference, description);

    private static TransferPaymentCandidate Transfer(decimal amount, string? reference = "TRX-0012", int dayOffset = 0) =>
        new(Guid.NewGuid(), Day.AddDays(dayOffset), amount, reference);

    // ── U-G1 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_G1_OneCandidateWithEqualAmount_DateWithinThreeDays_AndAReferenceToken_IsMatched()
    {
        var line = Line(1500m, dayOffset: 2, reference: "NEFT trx0012 dhaka");
        var payment = Transfer(1500m, "TRX-00 12");

        var suggestion = Assert.Single(BankMatchSuggester.Suggest([line], [payment], 3));

        Assert.Equal(BankMatchOutcomeKind.Matched, suggestion.Outcome);
        Assert.Equal(payment.PaymentId, suggestion.PaymentId);
    }

    // ── U-G2 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_G2_TwoEqualCandidates_AnAmountOffByAPenny_AndADateFourDaysOff_AreNotMatched()
    {
        var ambiguous = Line(500m);
        var first = Transfer(500m);
        var second = Transfer(500m);
        Assert.Equal(BankMatchOutcomeKind.Ambiguous, Assert.Single(BankMatchSuggester.Suggest([ambiguous], [first, second], 3)).Outcome);

        var penny = Line(500.01m);
        Assert.Equal(BankMatchOutcomeKind.None, Assert.Single(BankMatchSuggester.Suggest([penny], [Transfer(500m)], 3)).Outcome);

        var late = Line(500m, dayOffset: 4);
        Assert.Equal(BankMatchOutcomeKind.None, Assert.Single(BankMatchSuggester.Suggest([late], [Transfer(500m)], 3)).Outcome);
    }

    [Fact]
    public void ThePaymentThatIsTheOnlyCandidateOfTwoLines_IsAmbiguousForBoth_NeverMatchedTwice()
    {
        var payment = Transfer(300m);
        var suggestions = BankMatchSuggester.Suggest([Line(300m), Line(300m)], [payment], 3);

        Assert.All(suggestions, s => Assert.Equal(BankMatchOutcomeKind.Ambiguous, s.Outcome));
    }

    // ── U-G3 ────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("TRX-00 12", "trx0012")]
    [InlineData("Trx 0012", "TRX-0012")]
    [InlineData("ref:  A-B_C", "refabc")]
    public void U_G3_ReferencesAreNormalised_CaseSpacesAndDashes(string left, string right) =>
        Assert.Equal(BankMatchSuggester.Normalise(left), BankMatchSuggester.Normalise(right));

    [Fact]
    public void AReferenceTooShortToBeATokenOrMissing_NeverMatches()
    {
        Assert.Equal(BankMatchOutcomeKind.None, Assert.Single(BankMatchSuggester.Suggest([Line(100m, reference: "ab")], [Transfer(100m, "ab")], 3)).Outcome);
        Assert.Equal(BankMatchOutcomeKind.None, Assert.Single(BankMatchSuggester.Suggest([Line(100m)], [Transfer(100m, null)], 3)).Outcome);
    }

    // ── U-G6 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_G6_ACollectionOf1200_Remitted1000Then150_HasAShortfallOf50()
    {
        Assert.Equal(CodCollectionStatus.Unremitted, CodShortfall.StatusOf(1200m, 0m));
        Assert.Equal(0m, CodShortfall.ShortfallOf(1200m, 0m));

        Assert.Equal(200m, CodShortfall.ShortfallOf(1200m, 1000m));
        Assert.Equal(50m, CodShortfall.ShortfallOf(1200m, 1000m + 150m));
        Assert.Equal(CodCollectionStatus.Shortfall, CodShortfall.StatusOf(1200m, 1150m));

        Assert.Equal(CodCollectionStatus.Remitted, CodShortfall.StatusOf(1200m, 1200m));
        Assert.Equal(0m, CodShortfall.ShortfallOf(1200m, 1200m));
    }

    [Fact]
    public void ACollection_IsOverdue_OnlyWhileNotFullyRemittedAndPastTheDueWindow()
    {
        Assert.True(CodShortfall.IsOverdue(Day.AddDays(-8), Day, 7, 100m, 0m));
        Assert.False(CodShortfall.IsOverdue(Day.AddDays(-7), Day, 7, 100m, 0m));
        Assert.False(CodShortfall.IsOverdue(Day.AddDays(-30), Day, 7, 100m, 100m));
    }

    // ── COD remittance aggregate ────────────────────────────────────────────────────────────────────
    private static CodRemittanceDto Remittance(decimal amount, params (Guid Payment, decimal Amount)[] lines) =>
        new("PATHAO", Day, amount, "BDT", RemittanceMethod.Cash, null, lines.Select(l => new CodRemittanceLineInput(l.Payment, l.Amount)).ToList(), Now, BillingTestData.Actor());

    [Fact]
    public void ARemittance_RecordsItsLines_AndReportsWhatNoLineCovers()
    {
        var remittance = new CodRemittanceAggregate(Guid.NewGuid());
        remittance.Record(Guid.NewGuid(), Remittance(1400m, (Guid.NewGuid(), 500m), (Guid.NewGuid(), 600m)));

        Assert.Equal(2, remittance.Lines.Count);
        Assert.Equal(300m, remittance.Unapplied);
        Assert.Equal("PATHAO", remittance.CourierCode);
    }

    [Fact]
    public void ARemittance_RefusesBadAmounts_DuplicateCollections_AndMoreCoveredThanReceived()
    {
        var one = Guid.NewGuid();
        string Refused(CodRemittanceDto dto) => ErrorOf(() => new CodRemittanceAggregate(Guid.NewGuid()).Record(Guid.NewGuid(), dto));

        Assert.Equal(BusinessViolationCodes.InvalidAmount, Refused(Remittance(0m, (one, 1m))));
        Assert.Equal(BusinessViolationCodes.InvalidAmount, Refused(Remittance(100m, (one, 0m))));
        Assert.Equal(BusinessViolationCodes.RemittanceExceedsCollection, Refused(Remittance(100m, (one, 60m), (one, 40m))));
        Assert.Equal(BusinessViolationCodes.RemittanceExceedsCollection, Refused(Remittance(100m, (one, 60m), (Guid.NewGuid(), 50m))));
        Assert.Equal(BusinessViolationCodes.CollectionNotCod, Refused(Remittance(100m)));
    }

    [Fact]
    public void ARemittanceReference_IsNeverACardNumber()
    {
        var dto = Remittance(100m, (Guid.NewGuid(), 100m)) with { Reference = "4111 1111 1111 1111" };

        Assert.Equal(BusinessViolationCodes.ReferenceLooksLikeCardNumber, ErrorOf(() => new CodRemittanceAggregate(Guid.NewGuid()).Record(Guid.NewGuid(), dto)));
    }

    [Fact]
    public void ARemittanceCourierCode_IsNeverACardNumber()
    {
        var dto = Remittance(100m, (Guid.NewGuid(), 100m)) with { CourierCode = "5555555555554444" };

        Assert.Equal(BusinessViolationCodes.ReferenceLooksLikeCardNumber, ErrorOf(() => new CodRemittanceAggregate(Guid.NewGuid()).Record(Guid.NewGuid(), dto)));
    }

    // ── Bank statement aggregate ────────────────────────────────────────────────────────────────────
    private static BankStatementImportDto Statement(params BankStatementLineInput[] lines) =>
        new("Main account", Day.AddDays(-30), Day, "statement.csv", new string('a', 64), lines, Now, BillingTestData.Actor());

    private static BankStatementLineInput Credit(int row, decimal amount, string? reference = null, string? description = null) =>
        new(row, Day, amount, BankEntryDirection.Credit, reference, description, null);

    [Fact]
    public void U_G5_ALuhnValidDigitRun_InADescriptionOrReference_IsStoredMasked()
    {
        var statement = new BankStatementAggregate(Guid.NewGuid());
        statement.Import(Guid.NewGuid(), Statement(Credit(2, 100m, "paid by 4111 1111 1111 1111", "POS 4111111111111111 refund")));

        var line = Assert.Single(statement.Lines);
        Assert.Contains("••••1111", line.Reference);
        Assert.Contains("••••1111", line.Description);
        Assert.DoesNotContain("4111", line.Reference);
        Assert.DoesNotContain("4111", line.Description);
    }

    [Fact]
    public void ATwelveDigitReferenceThatIsNotACardNumber_IsKeptAsIs()
    {
        var statement = new BankStatementAggregate(Guid.NewGuid());
        statement.Import(Guid.NewGuid(), Statement(Credit(2, 100m, "TRX 20260101 0012")));

        Assert.Equal("TRX 20260101 0012", Assert.Single(statement.Lines).Reference);
    }

    [Fact]
    public void AStatement_RefusesNoLines_ABackwardsPeriod_ABadFingerprint_TooManyLines_AndANonPositiveAmount()
    {
        string Refused(BankStatementImportDto dto) => ErrorOf(() => new BankStatementAggregate(Guid.NewGuid()).Import(Guid.NewGuid(), dto));

        Assert.Equal(BusinessViolationCodes.StatementMalformed, Refused(Statement()));
        Assert.Equal(BusinessViolationCodes.StatementMalformed, Refused(Statement(Credit(2, 100m)) with { PeriodFrom = Day, PeriodTo = Day.AddDays(-1) }));
        Assert.Equal(BusinessViolationCodes.StatementMalformed, Refused(Statement(Credit(2, 100m)) with { SourceSha256 = "short" }));
        Assert.Equal(BusinessViolationCodes.StatementMalformed, Refused(Statement(Credit(2, 0m))));
        Assert.Equal(BusinessViolationCodes.StatementTooLarge,
            Refused(Statement(Enumerable.Range(2, BillingRules.MaxStatementLines + 1).Select(i => Credit(i, 1m)).ToArray())));
    }

    // ── Bank match aggregate ────────────────────────────────────────────────────────────────────────
    [Fact]
    public void AMatch_CanBeUndoneOnce_WithAReason_AndKeepsItsRow()
    {
        var match = new BankMatchAggregate(Guid.NewGuid());
        match.Match(new BankMatchDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BankMatchMethod.Manual, Now, BillingTestData.Actor()));
        Assert.True(match.IsActive);

        Assert.Equal(BusinessViolationCodes.ReasonRequired, ErrorOf(() => match.Unmatch(" ", Now, BillingTestData.Actor())));

        match.Unmatch("Wrong customer", Now, BillingTestData.Actor());
        Assert.False(match.IsActive);
        Assert.Equal("Wrong customer", match.UnmatchReason);
        Assert.Equal(BusinessViolationCodes.BankMatchNotFound, ErrorOf(() => match.Unmatch("again", Now, BillingTestData.Actor())));
    }
}

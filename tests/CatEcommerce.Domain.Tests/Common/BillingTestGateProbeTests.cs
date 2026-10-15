using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-BIL-001 TEST gate additions (sheet rows XU-1..XU-5): the ageing bucket edges, which had no unit test of their own, and a wider matrix
/// for the C-5 card-number guard than the developer's. Characters that are invisible or look alike are written as escapes so the file says exactly
/// what it tests.</summary>
public sealed class BillingTestGateProbeTests
{
    private static readonly DateOnly AsOf = new(2026, 10, 7);

    // ── XU-1 ────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(-5, "Current")]   // not yet due
    [InlineData(0, "Current")]    // due today is not overdue
    [InlineData(1, "Days1To30")]
    [InlineData(30, "Days1To30")]
    [InlineData(31, "Days31To60")]
    [InlineData(60, "Days31To60")]
    [InlineData(61, "Days61To90")]
    [InlineData(90, "Days61To90")]
    [InlineData(91, "Over90")]
    [InlineData(4000, "Over90")]
    public void XU_1_AgeingBucket_EdgesAreInclusiveAtTheUpperBound(int daysOverdue, string expected)
    {
        Assert.Equal(expected, AgeingCalculator.BucketFor(AsOf.AddDays(-daysOverdue), AsOf).DisplayName);
    }

    [Fact]
    public void XU_1_Totals_PutEachInvoiceInExactlyOneBucket_AndSumToTheOutstanding()
    {
        var customer = Guid.NewGuid();
        var inputs = new[] { 0, 1, 30, 31, 60, 61, 90, 91 }
            .Select((d, i) => new AgeingInput(customer, Guid.NewGuid(), AsOf.AddDays(-d), 100m + i))
            .ToList();

        var totals = AgeingCalculator.Totals(inputs, AsOf);

        Assert.Equal(100m, totals.Current);
        Assert.Equal(101m + 102m, totals.Days1To30);
        Assert.Equal(103m + 104m, totals.Days31To60);
        Assert.Equal(105m + 106m, totals.Days61To90);
        Assert.Equal(107m, totals.Over90);
        Assert.Equal(inputs.Sum(i => i.Outstanding), totals.Total);
    }

    [Fact]
    public void XU_1_ByCustomer_OrdersByOldestDueThenCustomerId_Deterministically()
    {
        var a = new Guid("00000000-0000-0000-0000-00000000000a");
        var b = new Guid("00000000-0000-0000-0000-00000000000b");
        var rows = AgeingCalculator.ByCustomer(
        [
            new AgeingInput(b, Guid.NewGuid(), AsOf.AddDays(-10), 10m),
            new AgeingInput(a, Guid.NewGuid(), AsOf.AddDays(-10), 20m),
            new AgeingInput(Guid.NewGuid(), Guid.NewGuid(), AsOf.AddDays(-50), 5m),
        ], AsOf);

        Assert.Equal(AsOf.AddDays(-50), rows[0].OldestDueOn);
        Assert.Equal([a, b], rows.Skip(1).Select(r => r.CustomerId));
    }

    // ── XU-2 ────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("4111 1111 1111 1111")]                                  // NBSP, as pasted from a PDF
    [InlineData("4111 1111 1111 1111")]                                  // narrow no-break space
    [InlineData("4111 1111 1111 1111")]                                  // em space
    [InlineData("٤١١١ ١١١١ ١١١١ ١١١١")] // Arabic-Indic digits
    [InlineData("৪১১১-১১১১-১১১১-১১১১")] // Bengali digits, dashes
    [InlineData("4111 1111 1111 1111\n")]
    [InlineData("tx\n4111\n1111\n1111\n1111\nok")]                                     // one group per line
    [InlineData("charged 4111111111111111 twice")]
    [InlineData("5555 5555 5555 4444")]                                               // MasterCard test number
    [InlineData("3782 822463 10005")]                                                 // Amex 15 digits, 4-6-5 grouping
    [InlineData("6011 1111 1111 1117")]
    public void XU_2_ACardNumber_IsRefused_AndMasked_InAnyOfTheseForms(string text)
    {
        Assert.True(CardNumberGuard.LooksLikeCardNumber(text), "not refused: " + Escape(text));

        var masked = CardNumberGuard.MaskCardNumbers(text);
        Assert.False(CardNumberGuard.LooksLikeCardNumber(masked), "still looks like a card number after masking: " + Escape(masked));
        Assert.Contains("••••", masked);
    }

    // ── XU-3 (probe) ────────────────────────────────────────────────────────────────────────────────
    // Separators the guard's own contract does not name (whitespace - . / _), but which a person or a spreadsheet export can produce.
    // OBSERVED FAIL on 2026-10-07 (15 of 15 inline cases): none is refused. Skipped so the branch build stays green; remove the Skip when
    // the finding (TEST gate F-1, MEDIUM, docs/gates/HO-BIL-001-TEST.md) is fixed.
    [Theory]
    [InlineData("4111,1111,1111,1111")]
    [InlineData("4111, 1111, 1111, 1111")]
    [InlineData("4111:1111:1111:1111")]
    [InlineData("4111;1111;1111;1111")]
    [InlineData("4111|1111|1111|1111")]
    [InlineData("4111–1111–1111–1111")]                                  // en dash
    [InlineData("4111—1111—1111—1111")]                                  // em dash
    [InlineData("4111‑1111‑1111‑1111")]                                  // non-breaking hyphen
    [InlineData("4111−" + "1111−" + "1111−" + "1111")]                    // minus sign
    [InlineData("4111​1111​1111​1111")]                                  // zero-width space
    [InlineData("4111­1111­1111­1111")]                                  // soft hyphen
    [InlineData("4111‍1111‍1111‍1111")]                                  // zero-width joiner
    [InlineData("(4111) (1111) (1111) (1111)")]
    [InlineData("4111*1111*1111*1111")]
    public void XU_3_ACardNumberWithAnUnusualSeparator_IsRefused(string text)
    {
        Assert.True(CardNumberGuard.LooksLikeCardNumber(text), "NOT REFUSED: " + Escape(text));
    }

    // ── XU-4 ────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("01712345678")]                                  // a mobile number
    [InlineData("+880 1712 345678 and 01812 345678")]            // two numbers, 13 digits each run but not one card
    [InlineData("2026-10-07T14:30:00.000+06:00")]
    [InlineData("c1a7e3d4-4111-4111-9111-111111111111")]          // a GUID that carries 4111 groups
    [InlineData("123456789012")]                                  // 12 digits
    [InlineData("1234567890123")]                                 // 13 digits failing Luhn
    [InlineData("TRX 4111 1111 1111 1112")]                       // Luhn fails
    [InlineData("INV-MAIN-2627-000123 CN-MAIN-2627-000007")]
    public void XU_4_OrdinaryDigitRuns_AreNotRefused(string text)
    {
        Assert.False(CardNumberGuard.LooksLikeCardNumber(text), "WRONGLY REFUSED: " + Escape(text));
        Assert.Equal(text, CardNumberGuard.MaskCardNumbers(text));
    }

    // ── XU-5 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void XU_5_Mask_KeepsOnlyTheLastFour_AndLeavesTheTextAroundIt()
    {
        Assert.Equal("a ••••1111 b", CardNumberGuard.MaskCardNumbers("a 4111  1111\t1111 - 1111 b"));
        Assert.Equal("paid ••••1111 now", CardNumberGuard.MaskCardNumbers("paid ৪১১১ ১১১১ ১১১১ ১১১১ now"));
        Assert.Equal("ref ••••4444 and ••••0005", CardNumberGuard.MaskCardNumbers("ref 5555 5555 5555 4444 and 3782 822463 10005"));
    }

    [Fact]
    public void XU_5_Mask_NeverLeavesA13DigitStretchBehind()
    {
        var text = "x 4111 1111 1111 1111 y 5555-5555-5555-4444 z 4111111111111111 w";
        var masked = CardNumberGuard.MaskCardNumbers(text);

        Assert.DoesNotMatch(@"\d{5,}", masked);
        Assert.False(CardNumberGuard.LooksLikeCardNumber(masked));
    }

    // ── XU-6 (probe: false refusals) ────────────────────────────────────────────────────────────────
    // A 13 to 19 digit run that passes Luhn is refused whatever it is. One 13-digit run in ten passes, and two everyday things are 13 digits: a Bangladeshi
    // mobile number with its country code (as written in a bKash or Nagad reference) and our own document number when the branch code ends in digits.
    // OBSERVED FAIL on 2026-10-07 (3 of 3). Skipped so the build stays green; unskip when TEST gate F-5 (LOW) is fixed.
    [Theory]
    [InlineData("bKash 8801712345605")]
    [InlineData("bKash 8801712345613")]
    [InlineData("bKash 8801912345602")]
    [InlineData("Reversal of INV-DHK001-2627-000123")]
    [InlineData("INV-DHK001-2627-000123")]
    [InlineData("CN-DHK001-2627-000045 for INV-DHK001-2627-000123")]
    [InlineData("bKash +8801712345605")]
    [InlineData("bKash +880 1712 345605")]
    public void XU_6_AMobileNumberOrOurOwnDocumentNumber_IsNotACardNumber(string text)
    {
        Assert.False(CardNumberGuard.LooksLikeCardNumber(text), "WRONGLY REFUSED: " + text);
    }

    // ── F-1 follow-up: what the guard deliberately does NOT join ────────────────────────────────────
    // A letter between digit groups ends the run. Joining them would refuse every reference that mixes letters and numbers; the cost is that
    // a card number split by letters (4111x1111x1111x1111) is not seen. That is the trade-off chosen in the F-1 fix round; it is reported, not hidden.
    [Theory]
    [InlineData("4111x1111x1111x1111")]
    [InlineData("4111 a 1111 b 1111 c 1111")]
    public void XU_3_ALetterBetweenGroups_DeliberatelyDoesNotJoinThem(string text) =>
        Assert.False(CardNumberGuard.LooksLikeCardNumber(text), "A letter must not join digit groups: " + Escape(text));

    // A supplementary-plane digit (mathematical bold 4 = U+1D7D2) is two UTF-16 chars; the mask keeps its index mapping.
    [Fact]
    public void XU_3_ASupplementaryPlaneDigitCardNumber_IsRefused_AndMaskedInPlace()
    {
        var text = "pay 𝟒111 1111 1111 1111 now";

        Assert.True(CardNumberGuard.LooksLikeCardNumber(text));
        Assert.Equal("pay ••••1111 now", CardNumberGuard.MaskCardNumbers(text));
    }

    [Fact]
    public void XU_3_AMobileWithCountryCode_IsNotACardNumber_ButAThirteenDigitNonMobileStillIs()
    {
        Assert.False(CardNumberGuard.LooksLikeCardNumber("8801712345605"));
        Assert.True(CardNumberGuard.LooksLikeCardNumber("4222222222222")); // the well-known 13-digit test card
    }

    [Fact]
    public void XU_6_ADocumentNumberShapedString_CannotHideACardNumber()
    {
        // The exemption needs consecutive financial-year digits; an arbitrary 13-digit string in the same shape is still refused.
        Assert.True(CardNumberGuard.LooksLikeCardNumber("INV-A422-2222-222222"));
    }

    private static string Escape(string text) =>
        string.Concat(text.Select(c => c < 0x20 || c > 0x7E ? $"\\u{(int)c:X4}" : c.ToString()));
}

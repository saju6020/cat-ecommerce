using System.Text;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-BIL-001 follow-up BIL-001-FU1 (backlog row 1, ACCEPTANCE M1): a digit group in front of a card number must not make the mask hide the
/// wrong digits. The mask used to take the leftmost-longest Luhn window, so <c>37085610 4003 3371 5286 9561</c> (the prefix plus the card's first
/// groups happens to pass Luhn) became <c>3••••3371 5286 9561</c> and 13 of the card's 16 digits stayed readable. Now every window is evaluated and
/// the overlapping ones are merged.</summary>
public sealed class CardNumberMaskPrefixTests
{
    [Fact]
    public void TheAcceptanceExample_KeepsOnlyTheLastFourOfTheCard()
    {
        Assert.Equal("TRF 3••••9561", CardNumberGuard.MaskCardNumbers("TRF 37085610 4003 3371 5286 9561"));
        Assert.True(CardNumberGuard.LooksLikeCardNumber("TRF 37085610 4003 3371 5286 9561"));
    }

    [Theory]
    [InlineData("a 4111 1111 1111 1111 b", "a ••••1111 b")]
    [InlineData("𝟗𝟒111111111111111", "𝟗••••1111")]            // a supplementary-plane stray digit glued to the front: the offset steps past its filler
    [InlineData("94111111111111111", "9••••1111")]
    [InlineData("two 4111111111111111 and 5555555555554444", "two ••••1111 and ••••4444")]
    public void KnownShapes(string text, string expected)
    {
        Assert.Equal(expected, CardNumberGuard.MaskCardNumbers(text));
    }

    [Fact]
    public void ASupplementaryPlaneDigitGluedToTheFront_IsNowRefused()
    {
        Assert.True(CardNumberGuard.LooksLikeCardNumber("𝟗𝟒111111111111111"));
        Assert.True(CardNumberGuard.LooksLikeCardNumber("94111111111111111"));
    }

    [Fact]
    public void TheMaskKeepsOneToOneIndexing_WithSupplementaryPlaneDigits()
    {
        // The text around a masked stretch comes back exactly as written, so the filler mapping cannot have shifted.
        Assert.Equal("before ••••1111 after 𝟏𝟐", CardNumberGuard.MaskCardNumbers("before 𝟒𝟏𝟏𝟏 𝟏𝟏𝟏𝟏 𝟏𝟏𝟏𝟏 𝟏𝟏𝟏𝟏 after 𝟏𝟐"));
    }

    // ── Property ────────────────────────────────────────────────────────────────────────────────────
    private static readonly string[] Separators = [" ", "-", ".", "/", "_", ", ", "; ", " | ", ": ", "", " - "];
    private static readonly string[] Labels = ["TRF ", "POS ", "REF:", "bKash ", "", "Payment "];

    /// <summary>Thousands of generated inputs: a label, a prefix number (4 to 12 digits, the shapes a bank reference, date or mobile number takes), a
    /// separator, then a Luhn-valid 16-digit card in four groups of four joined by a (possibly different) separator, and a trailing word.
    /// Threshold: after masking, no <b>5</b> consecutive digits of the original card may survive. The permitted leak is the last four, so 5 is the
    /// smallest run that is already too many; the old defect left 13.</summary>
    [Fact]
    public void Property_NoCardRunOfFiveOrMoreDigitsSurvivesMasking_AndTheLastFourRemain()
    {
        var random = new Random(20261007);
        const int cases = 20000;
        var failures = new List<string>();

        for (var n = 0; n < cases; n++)
        {
            var card = LuhnCard(random);
            var prefix = RandomDigits(random, random.Next(4, 13));
            var groupSeparator = Separators[random.Next(Separators.Length)];
            var joinSeparator = Separators[random.Next(Separators.Length)];
            if (joinSeparator.Length == 0) joinSeparator = " ";

            var formatted = string.Join(groupSeparator, Enumerable.Range(0, 4).Select(i => card.Substring(i * 4, 4)));
            var text = Labels[random.Next(Labels.Length)] + prefix + joinSeparator + formatted + (random.Next(2) == 0 ? " DHAKA" : "");

            // A prefix that by coincidence already contains one of the card's 5-digit runs would be reported as a card leak; it is not one.
            if (Enumerable.Range(0, 8).Any(i => prefix.Contains(card.Substring(i, 5), StringComparison.Ordinal))) continue;

            var masked = CardNumberGuard.MaskCardNumbers(text);
            if (!Survives(masked, card)) continue;

            failures.Add(text + "  =>  " + masked);
        }

        Assert.True(failures.Count == 0, $"{failures.Count} of {cases} leaked; first: " + string.Join(" || ", failures.Take(5)));
    }

    /// <summary>True when the masked text still shows 5 or more consecutive card digits, or lost the last four. Separators are removed, the mask dots
    /// are kept so they break a run.</summary>
    private static bool Survives(string masked, string card)
    {
        var compact = new string(masked.Where(c => char.IsAsciiDigit(c) || c == '•').ToArray());
        if (!compact.Contains("••••" + card[^4..], StringComparison.Ordinal)) return true;

        for (var i = 0; i + 5 <= card.Length; i++)
        {
            // The last four are allowed to be read; a 5-run that ends at the end of the card includes a masked digit, so only runs inside the first 12 count.
            if (i + 5 > 12) break;
            if (compact.Contains(card.Substring(i, 5), StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private static string RandomDigits(Random random, int length)
    {
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++) builder.Append((char)('0' + random.Next(10)));
        return builder.ToString();
    }

    private static string LuhnCard(Random random)
    {
        var body = RandomDigits(random, 15);
        var sum = 0;
        for (var i = 0; i < 15; i++)
        {
            var digit = body[14 - i] - '0';
            if (i % 2 == 0) { digit *= 2; if (digit > 9) digit -= 9; }
            sum += digit;
        }

        return body + (char)('0' + (10 - sum % 10) % 10);
    }
}

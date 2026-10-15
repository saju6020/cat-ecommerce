using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Helpers;

/// <summary>HO-IMP-001 section 15 U-X4 -- the CSV writer applies HO-ADM-001 ADR-051's injection guard to text, and leaves typed numbers alone.</summary>
public sealed class InventoryCsvTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")", "\"'=HYPERLINK(\"\"http://evil\"\")\"")]
    [InlineData("+1", "\"'+1\"")]
    [InlineData("-2", "\"'-2\"")]
    [InlineData("@x", "\"'@x\"")]
    [InlineData("  =1+1", "\"'  =1+1\"")]
    [InlineData("\t=1", "\"'\t=1\"")]
    [InlineData("plain", "\"plain\"")]
    [InlineData("a=b", "\"a=b\"")]
    public void U_X4_ATextCellThatCouldBeAFormulaIsPrefixedWithAnApostrophe(string input, string expected) => Assert.Equal(expected, InventoryCsv.Cell(input));

    [Fact]
    public void U_X4_TypedNumbersDatesAndGuidsAreWrittenAsInvariantTextWithoutTheGuard()
    {
        Assert.Equal("\"-5.250\"", InventoryCsv.Cell(-5.250m));
        Assert.Equal("\"-3\"", InventoryCsv.Cell(-3));
        Assert.Equal("\"2026-10-07\"", InventoryCsv.Cell(new DateOnly(2026, 10, 7)));
        Assert.Equal("\"true\"", InventoryCsv.Cell(true));
        Assert.Equal("\"\"", InventoryCsv.Cell(null));
    }

    [Fact]
    public void TheHeaderRowAndTheRowsAreCrLfTerminated_AndQuotesAreDoubled()
    {
        var csv = InventoryCsv.Build(["A", "B"], [new object?[] { "say \"hi\"", 1 }]);

        Assert.Equal("\"A\",\"B\"\r\n\"say \"\"hi\"\"\",\"1\"\r\n", csv);
    }
}

using System.Text;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Billing;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Tests.Billing;

/// <summary>HO-BIL-001 section 15 U-G4 -- the canonical bank statement CSV is parsed in memory: strict, row-numbered, never evaluating a cell.</summary>
public sealed class BankStatementCsvParserTests
{
    private const string Header = "date,amount,direction,reference,description,bankTransactionId";
    private readonly IBankStatementCsvParser _parser = new BankStatementCsvParser();

    private BankStatementParseResult Parse(string text) => _parser.Parse(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void U_G4_AValidFile_YieldsItsLines_AndTheFingerprintOfItsBytes()
    {
        var text = $"{Header}\n2026-10-01,1500.00,C,TRX-1,Customer payment,B001\r\n2026-10-02,200,D,,Bank charge,\n";

        var result = Parse(text);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(new DateOnly(2026, 10, 1), result.Lines[0].TransactionDate);
        Assert.Equal(1500.00m, result.Lines[0].Amount);
        Assert.Equal(BankEntryDirection.Credit, result.Lines[0].Direction);
        Assert.Equal("TRX-1", result.Lines[0].Reference);
        Assert.Equal(BankEntryDirection.Debit, result.Lines[1].Direction);
        Assert.Null(result.Lines[1].Reference);
        Assert.Equal(2, result.Lines[0].LineNumber); // the header is row 1
        Assert.Equal(64, result.Sha256.Length);
        Assert.Equal(Parse(text).Sha256, result.Sha256);
        Assert.NotEqual(Parse(text + "\n").Sha256, result.Sha256);
    }

    [Fact]
    public void U_G4_AMissingHeader_IsMalformed()
    {
        var result = Parse("2026-10-01,1500.00,C,TRX-1,x,\n");

        Assert.False(result.IsValid);
        Assert.Equal(1, Assert.Single(result.Errors).RowNumber);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void U_G4_ABadDateOnRow7_NamesRow7_AndNotTheContent()
    {
        var rows = Enumerable.Range(1, 5).Select(i => $"2026-10-0{i},10.00,C,R{i},d,");
        var text = $"{Header}\n{string.Join("\n", rows)}\n2026-13-45,10.00,C,SECRET-REF,d,\n";

        var result = Parse(text);

        var error = Assert.Single(result.Errors);
        Assert.Equal(7, error.RowNumber);
        Assert.DoesNotContain("SECRET-REF", error.Message);
        Assert.DoesNotContain("2026-13-45", error.Message);
    }

    [Theory]
    [InlineData("\"1,500.00\"")]   // a thousands separator
    [InlineData("1500.005")]       // three decimals
    [InlineData("-5.00")]          // a sign
    [InlineData("0")]              // not positive
    [InlineData("12e3")]           // exponent
    [InlineData("")]               // missing
    public void U_G4_AnAmountThatIsNotPlain_IsRefused(string amount)
    {
        var result = Parse($"{Header}\n2026-10-01,{amount},C,R,d,\n");

        Assert.False(result.IsValid);
        Assert.Equal(2, Assert.Single(result.Errors).RowNumber);
    }

    [Fact]
    public void U_G4_AFileOverOneMiB_IsTooLarge_AndOneMiBExactlyIsNot()
    {
        var over = new byte[BillingRules.MaxStatementBytes + 1];
        Assert.True(_parser.Parse(over).TooLarge);

        var prefix = Encoding.UTF8.GetBytes($"{Header}\n2026-10-01,1.00,C,R,d,\n");
        var exactly = prefix.Concat(Enumerable.Repeat((byte)'\n', BillingRules.MaxStatementBytes - prefix.Length)).ToArray();
        Assert.Equal(BillingRules.MaxStatementBytes, exactly.Length);
        Assert.False(_parser.Parse(exactly).TooLarge);
    }

    [Fact]
    public void MoreThanFiveThousandRows_IsTooLarge()
    {
        var builder = new StringBuilder(Header).Append('\n');
        for (var i = 0; i < BillingRules.MaxStatementLines + 1; i++) builder.Append("2026-10-01,1.00,C,R,d,\n");

        Assert.True(Parse(builder.ToString()).TooLarge);
    }

    [Fact]
    public void QuotedValues_WithCommasQuotesAndLineBreaks_AreOneCell_AndFormulasStayText()
    {
        var text = $"{Header}\n2026-10-01,10.00,C,\"A,B\",\"he said \"\"hi\"\"\nsecond line\",\n2026-10-02,5.00,D,=cmd|' /C calc'!A0,+SUM(A1),\n";

        var result = Parse(text);

        Assert.True(result.IsValid);
        Assert.Equal("A,B", result.Lines[0].Reference);
        Assert.Equal("he said \"hi\"\nsecond line", result.Lines[0].Description);
        Assert.Equal(3, result.Lines[1].LineNumber); // the quoted line break does not shift row numbers
        Assert.StartsWith("=cmd", result.Lines[1].Reference);
        Assert.Equal("+SUM(A1)", result.Lines[1].Description);
    }

    [Fact]
    public void AFileThatIsNotUtf8_AnUnclosedQuote_AndADirectionOtherThanCOrD_AreRefused()
    {
        Assert.False(_parser.Parse(new byte[] { 0xFF, 0xFE, 0x00, 0xD8, 0x41 }).IsValid);
        Assert.False(Parse($"{Header}\n2026-10-01,1.00,C,\"open,d,\n").IsValid);
        Assert.False(Parse($"{Header}\n2026-10-01,1.00,X,R,d,\n").IsValid);
    }

    [Fact]
    public void ABomIsIgnored_AnHeaderMayComeInAnyOrderWithOptionalColumns_AndABlankLineIsSkipped()
    {
        var result = Parse("﻿direction,amount,date\nC,10.50,2026-10-01\n\nD,2.00,2026-10-02\n");

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(10.50m, result.Lines[0].Amount);
        Assert.Null(result.Lines[0].Description);
    }

    [Fact]
    public void AFileWithOnlyAHeader_HasNoDataRows()
    {
        var result = Parse(Header + "\n");

        Assert.False(result.IsValid);
        Assert.Empty(result.Lines);
    }
}

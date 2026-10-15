using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>
/// HO-BIL-001 section 12.2 / OQ-10 (ADR-189) -- parses the canonical statement CSV in memory: header <c>date,amount,direction,reference,description,
/// bankTransactionId</c> (the first three required, the rest optional, any order), RFC 4180 quoting, dates <c>yyyy-MM-dd</c>, amounts with
/// <c>.</c> and at most two decimals (no thousands separators), direction <c>C</c> or <c>D</c>. More than 1 MiB or more than 5 000 rows is
/// <i>too large</i>; bytes that are not UTF-8 are one file-level error. Errors name the row (the header is row 1) -- never the cell
/// content, which may hold anything. A cell that looks like a formula is kept as text; nothing is evaluated.
/// </summary>
public sealed class BankStatementCsvParser : IBankStatementCsvParser
{
    private static readonly string[] RequiredColumns = ["date", "amount", "direction"];
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public BankStatementParseResult Parse(ReadOnlyMemory<byte> content)
    {
        var sha = Convert.ToHexString(SHA256.HashData(content.Span)).ToLowerInvariant();

        if (content.Length > BillingRules.MaxStatementBytes)
            return new BankStatementParseResult(true, sha, [], []);

        string text;
        try
        {
            text = StrictUtf8.GetString(content.Span);
        }
        catch (DecoderFallbackException)
        {
            return Failed(sha, 0, "The file is not valid UTF-8 text.");
        }

        if (text.Length > 0 && text[0] == '﻿') text = text[1..];

        var records = ReadRecords(text, out var unterminatedAt);
        if (unterminatedAt is { } badRow)
            return Failed(sha, badRow, "A quoted value is not closed.");

        if (records.Count == 0)
            return Failed(sha, 1, "The header row is missing.");

        var header = records[0].Select(c => c.Trim().ToLowerInvariant()).ToList();
        var missing = RequiredColumns.Where(c => !header.Contains(c)).ToList();
        if (missing.Count > 0)
            return Failed(sha, 1, $"The header is missing: {string.Join(", ", missing)}.");

        if (records.Skip(1).Count(r => !(r.Count == 1 && string.IsNullOrWhiteSpace(r[0]))) > BillingRules.MaxStatementLines)
            return new BankStatementParseResult(true, sha, [], []);

        int Index(string name) => header.IndexOf(name);
        var dateAt = Index("date");
        var amountAt = Index("amount");
        var directionAt = Index("direction");
        var referenceAt = Index("reference");
        var descriptionAt = Index("description");
        var transactionAt = Index("banktransactionid");

        var lines = new List<BankStatementLineInput>();
        var errors = new List<BankStatementParseError>();

        for (var i = 1; i < records.Count; i++)
        {
            var row = i + 1;
            var cells = records[i];
            if (cells.Count == 1 && string.IsNullOrWhiteSpace(cells[0])) continue;

            string? Cell(int at) => at >= 0 && at < cells.Count ? cells[at] : null;

            if (!DateOnly.TryParseExact(Cell(dateAt)?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                errors.Add(new BankStatementParseError(row, "The date must be written yyyy-MM-dd."));
                continue;
            }

            var amountText = Cell(amountAt)?.Trim();
            if (string.IsNullOrEmpty(amountText) || !IsPlainAmount(amountText) ||
                !decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount <= 0m)
            {
                errors.Add(new BankStatementParseError(row, "The amount must be a positive number with a . decimal point, at most two decimals and no thousands separator."));
                continue;
            }

            var direction = Cell(directionAt)?.Trim().ToUpperInvariant() switch
            {
                "C" => BankEntryDirection.Credit,
                "D" => BankEntryDirection.Debit,
                _ => null,
            };
            if (direction is null)
            {
                errors.Add(new BankStatementParseError(row, "The direction must be C (credit) or D (debit)."));
                continue;
            }

            var reference = Clean(Cell(referenceAt));
            var description = Clean(Cell(descriptionAt));
            if (reference is { Length: > BillingRules.MaxReferenceLength } || description is { Length: > 500 })
            {
                errors.Add(new BankStatementParseError(row, "The reference or description is too long."));
                continue;
            }

            lines.Add(new BankStatementLineInput(row, date, amount, direction, reference, description, Clean(Cell(transactionAt))));
        }

        if (errors.Count == 0 && lines.Count == 0)
            errors.Add(new BankStatementParseError(1, "The statement has no data rows."));

        return new BankStatementParseResult(false, sha, errors.Count == 0 ? lines : [], errors);
    }

    private static BankStatementParseResult Failed(string sha, int row, string message) =>
        new(false, sha, [], [new BankStatementParseError(row, message)]);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Digits with at most one <c>.</c> and at most two decimals; a sign, a space or a comma makes it malformed.</summary>
    private static bool IsPlainAmount(string text)
    {
        var dot = text.IndexOf('.');
        var whole = dot < 0 ? text : text[..dot];
        var fraction = dot < 0 ? string.Empty : text[(dot + 1)..];

        return whole.Length > 0 && whole.All(char.IsAsciiDigit) && fraction.Length <= 2 && fraction.All(char.IsAsciiDigit) && (dot < 0 || fraction.Length > 0);
    }

    /// <summary>RFC 4180: comma separated, <c>"</c> quotes, <c>""</c> an escaped quote, line breaks allowed inside quotes. A row is a record, so
    /// a quoted line break does not shift later row numbers.</summary>
    private static List<List<string>> ReadRecords(string text, out int? unterminatedAt)
    {
        var records = new List<List<string>>();
        var cells = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        unterminatedAt = null;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else current.Append(c);

                continue;
            }

            switch (c)
            {
                case '"' when current.Length == 0:
                    inQuotes = true;
                    break;
                case ',':
                    cells.Add(current.ToString());
                    current.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    cells.Add(current.ToString());
                    current.Clear();
                    records.Add(cells);
                    cells = [];
                    break;
                default:
                    current.Append(c);
                    break;
            }
        }

        if (inQuotes) unterminatedAt = records.Count + 1;

        if (current.Length > 0 || cells.Count > 0)
        {
            cells.Add(current.ToString());
            records.Add(cells);
        }

        return records;
    }
}

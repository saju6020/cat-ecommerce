using System.Globalization;
using System.Text;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One printed line of a receipt: what was sold, in what quantity, at what price, and the line's own tax.</summary>
public sealed record ReceiptTextLine(string Description, int Quantity, string UnitPrice, string LineTotal, string? Discount, string? TaxNote);

/// <summary>Everything the 80 mm text receipt prints. Amounts are already formatted text (<c>1,927.50</c>); the renderer only lays them out.</summary>
public sealed record ReceiptText(
    string BranchName, IReadOnlyList<string> BranchAddress, IReadOnlyList<string> LegalIdentifiers, string InvoiceNumber, string OrderNumber, string IssuedOn, string Cashier,
    string CurrencyCode, IReadOnlyList<ReceiptTextLine> Lines, IReadOnlyList<(string Label, string Amount)> Totals, IReadOnlyList<(string Label, string Amount)> Tenders,
    string? ChangeDue, string Footer);

/// <summary>
/// HO-ORD-001 §8.11 (HW-1), US-ORD-16 AC1 -- the plain 48-column rendering of a receipt for an 80 mm thermal printer. Pure and culture-free. <b>No line is ever longer than
/// 48 characters</b>, totals are right-aligned to column 48, a description wraps onto continuation lines rather than being cut, and an amount is never split across lines.
/// The browser prints this text through the OS printer driver (no server-side printing, HW-1).
/// </summary>
public static class ReceiptTextRenderer
{
    public const int Columns = 48;

    public static string Render(ReceiptText r)
    {
        var text = new StringBuilder();
        foreach (var line in Wrap(r.BranchName, Columns)) text.AppendLine(Center(line));
        foreach (var line in r.BranchAddress.SelectMany(a => Wrap(a, Columns))) text.AppendLine(Center(line));
        foreach (var line in r.LegalIdentifiers.SelectMany(a => Wrap(a, Columns))) text.AppendLine(Center(line));
        text.AppendLine(Rule('='));

        AppendPair(text, "Invoice", r.InvoiceNumber);
        AppendPair(text, "Order", r.OrderNumber);
        AppendPair(text, "Date", r.IssuedOn);
        AppendPair(text, "Cashier", r.Cashier);
        text.AppendLine(Rule('-'));

        foreach (var line in r.Lines)
        {
            foreach (var part in Wrap(line.Description, Columns)) text.AppendLine(part);

            AppendPair(text, $"  {line.Quantity.ToString(CultureInfo.InvariantCulture)} x {line.UnitPrice}", line.LineTotal);
            if (line.Discount is not null) AppendPair(text, "  Discount", "-" + line.Discount);
            if (line.TaxNote is not null) AppendPair(text, "  " + line.TaxNote, string.Empty);
        }

        text.AppendLine(Rule('-'));
        foreach (var (label, amount) in r.Totals) AppendPair(text, label, amount);
        text.AppendLine(Rule('-'));
        foreach (var (label, amount) in r.Tenders) AppendPair(text, label, amount);
        if (r.ChangeDue is not null) AppendPair(text, "Change", r.ChangeDue);
        text.AppendLine(Rule('='));
        foreach (var line in Wrap(r.Footer, Columns)) text.AppendLine(Center(line));

        return text.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>Label on the left, amount on the right edge. A label too long to leave room for the amount is wrapped; the amount stays whole on the last line.</summary>
    private static void AppendPair(StringBuilder text, string label, string amount)
    {
        var room = Columns - (amount.Length == 0 ? 0 : amount.Length + 1);
        var parts = Wrap(label, Math.Max(room, 1)).ToList();
        for (var i = 0; i < parts.Count - 1; i++) text.AppendLine(parts[i]);

        var last = parts.Count == 0 ? string.Empty : parts[^1];
        text.AppendLine(amount.Length == 0 ? last : last.PadRight(Columns - amount.Length) + amount);
    }

    private static string Center(string value) => value.Length >= Columns ? value : new string(' ', (Columns - value.Length) / 2) + value;

    private static string Rule(char c) => new(c, Columns);

    /// <summary>Greedy word wrap to <paramref name="width"/>; a single word longer than the width is split.</summary>
    private static IEnumerable<string> Wrap(string value, int width)
    {
        var current = new StringBuilder();
        foreach (var word in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var rest = word;
            while (rest.Length > width)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                yield return rest[..width];
                rest = rest[width..];
            }

            if (current.Length > 0 && current.Length + 1 + rest.Length > width)
            {
                yield return current.ToString();
                current.Clear();
            }

            if (current.Length > 0) current.Append(' ');
            current.Append(rest);
        }

        if (current.Length > 0) yield return current.ToString();
    }
}

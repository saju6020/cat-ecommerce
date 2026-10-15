using System.Text;

namespace CatEcommerce.Domain.DomainServices;

public sealed record BankLineCandidateInput(Guid LineId, DateOnly Date, decimal Amount, string? Reference, string? Description);

public sealed record TransferPaymentCandidate(Guid PaymentId, DateOnly ReceivedOn, decimal Amount, string? Reference);

public enum BankMatchOutcomeKind
{
    Matched,
    Ambiguous,
    None,
}

public sealed record BankMatchSuggestion(Guid LineId, BankMatchOutcomeKind Outcome, Guid? PaymentId);

/// <summary>
/// HO-BIL-001 ADR-189 -- pure. A statement credit auto-matches a transfer payment only when <b>exactly
/// one</b> candidate has an equal amount, a received date within <c>toleranceDays</c> and a reference
/// token match (the payment's reference, normalised, appears in the line's reference or description,
/// normalised). A payment that is the single candidate of two lines is ambiguous for both -- never
/// matched twice. Anything else is left for a person.
/// </summary>
public static class BankMatchSuggester
{
    private const int MinReferenceLength = 3;

    public static IReadOnlyList<BankMatchSuggestion> Suggest(
        IReadOnlyList<BankLineCandidateInput> lines, IReadOnlyList<TransferPaymentCandidate> payments, int toleranceDays)
    {
        var candidatesByLine = lines.ToDictionary(
            l => l.LineId,
            l => payments.Where(p => IsCandidate(l, p, toleranceDays)).ToList());

        var lineCountByPayment = candidatesByLine
            .Where(kv => kv.Value.Count == 1)
            .GroupBy(kv => kv.Value[0].PaymentId)
            .ToDictionary(g => g.Key, g => g.Count());

        var suggestions = new List<BankMatchSuggestion>();
        foreach (var line in lines)
        {
            var candidates = candidatesByLine[line.LineId];
            if (candidates.Count == 0)
                suggestions.Add(new BankMatchSuggestion(line.LineId, BankMatchOutcomeKind.None, null));
            else if (candidates.Count > 1 || lineCountByPayment[candidates[0].PaymentId] > 1)
                suggestions.Add(new BankMatchSuggestion(line.LineId, BankMatchOutcomeKind.Ambiguous, null));
            else
                suggestions.Add(new BankMatchSuggestion(line.LineId, BankMatchOutcomeKind.Matched, candidates[0].PaymentId));
        }

        return suggestions;
    }

    private static bool IsCandidate(BankLineCandidateInput line, TransferPaymentCandidate payment, int toleranceDays)
    {
        if (line.Amount != payment.Amount) return false;
        if (Math.Abs(line.Date.DayNumber - payment.ReceivedOn.DayNumber) > toleranceDays) return false;

        var paymentToken = Normalise(payment.Reference);
        if (paymentToken.Length < MinReferenceLength) return false;

        return Normalise(line.Reference).Contains(paymentToken, StringComparison.Ordinal) ||
               Normalise(line.Description).Contains(paymentToken, StringComparison.Ordinal);
    }

    /// <summary>Lower-case letters and digits only, so <c>TRX-00 12</c> and <c>trx0012</c> are one token.</summary>
    public static string Normalise(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}

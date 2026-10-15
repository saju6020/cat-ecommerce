using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 section 8.1 / 11.4 C23 (US-BIL-06 AC2, ADR-189) -- one imported bank statement. The file itself is parsed in memory and never
/// kept; what is kept is its lines, with every description and reference <b>card-masked</b> before it is stored (C-5) and the SHA-256 of
/// the file so the same file cannot be imported twice. <b>Append-only</b>: one mutation, <see cref="Import"/>; what is matched to what
/// belongs to <see cref="BankMatchAggregate"/>, not to the statement.
/// </summary>
public sealed class BankStatementAggregate : AggregateRoot
{
    public const int MaxLabelLength = 100;
    public const int MaxFileNameLength = 255;
    public const int MaxDescriptionLength = 500;

    private readonly List<BankStatementLine> _lines = [];

    public Guid BranchId { get; private set; }
    public string BankAccountLabel { get; private set; } = default!;
    public DateOnly PeriodFrom { get; private set; }
    public DateOnly PeriodTo { get; private set; }
    public string SourceFileName { get; private set; } = default!;
    public string SourceSha256 { get; private set; } = default!;
    public Guid ImportedBy { get; private set; }
    public DateTimeOffset ImportedOn { get; private set; }
    public IReadOnlyList<BankStatementLine> Lines => _lines;

    private BankStatementAggregate() { }

    public BankStatementAggregate(Guid id) : base(id) { }

    public void Import(Guid branchId, BankStatementImportDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.BankAccountLabel) || dto.BankAccountLabel.Trim().Length > MaxLabelLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.StatementMalformed, "Name the bank account this statement belongs to.", nameof(dto.BankAccountLabel));
        BillingFreeText.RefuseCardNumber(dto.BankAccountLabel, nameof(dto.BankAccountLabel));

        if (dto.PeriodFrom > dto.PeriodTo)
            throw new DomainRuleViolationException(BusinessViolationCodes.StatementMalformed, "The statement period ends before it starts.", nameof(dto.PeriodTo));

        if (string.IsNullOrWhiteSpace(dto.SourceSha256) || dto.SourceSha256.Length != 64)
            throw new DomainRuleViolationException(BusinessViolationCodes.StatementMalformed, "The statement file could not be fingerprinted.", nameof(dto.SourceSha256));

        if (dto.Lines.Count == 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.StatementMalformed, "The statement has no lines.", nameof(dto.Lines));

        if (dto.Lines.Count > BillingRules.MaxStatementLines)
            throw new DomainRuleViolationException(BusinessViolationCodes.StatementTooLarge, $"A statement can have at most {BillingRules.MaxStatementLines} lines.", nameof(dto.Lines));

        if (dto.Lines.Any(l => l.Amount <= 0m || decimal.Round(l.Amount, 2) != l.Amount || l.Amount > BillingRules.MaxAmount))
            throw new DomainRuleViolationException(BusinessViolationCodes.StatementMalformed, "Every statement amount must be greater than zero with at most two decimals.", nameof(dto.Lines));

        BranchId = branchId;
        BankAccountLabel = dto.BankAccountLabel.Trim();
        PeriodFrom = dto.PeriodFrom;
        PeriodTo = dto.PeriodTo;
        SourceFileName = Truncate(Mask(dto.SourceFileName), MaxFileNameLength) ?? "statement.csv"; // an uploaded name is not the user's to choose: masked, not refused
        SourceSha256 = dto.SourceSha256.ToLowerInvariant();
        ImportedBy = dto.Actor.UserId;
        ImportedOn = dto.Now;

        foreach (var line in dto.Lines.OrderBy(l => l.LineNumber))
        {
            _lines.Add(new BankStatementLine(
                Guid.NewGuid(), line.LineNumber, line.TransactionDate, line.Amount, line.Direction,
                Truncate(Mask(line.Reference), BillingRules.MaxReferenceLength), Truncate(Mask(line.Description), MaxDescriptionLength),
                Truncate(Mask(line.BankTransactionId), BillingRules.MaxReferenceLength)));
        }

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new BankStatementImportedEvent
        {
            StatementId = Id, LineCount = _lines.Count, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext,
        });
        UpdateVersion();
    }

    /// <summary>A card-like digit run is stored with only its last four digits (C-5); the raw text is never kept.</summary>
    private static string? Mask(string? text) => string.IsNullOrEmpty(text) ? text : CardNumberGuard.MaskCardNumbers(text);

    private static string? Truncate(string? text, int max)
    {
        var trimmed = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        return trimmed is { Length: > 0 } && trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}

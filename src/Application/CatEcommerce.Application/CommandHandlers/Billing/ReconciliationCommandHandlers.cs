using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Billing.BillingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Billing;

public sealed record RecordedRemittanceResult(Guid RemittanceId, decimal Unapplied);

public sealed record ImportedStatementResult(Guid StatementId, int LineCount);

/// <summary>C24's result: lines matched now, lines with more than one possible payment, lines with none.</summary>
public sealed record AutoMatchResult(int Matched, int Ambiguous, int None);

/// <summary>
/// HO-BIL-001 section 11.4 C22 (US-BIL-06 AC1, ADR-189) -- every named payment must be an accepted cash-on-delivery collection of <b>this</b> courier at
/// this branch, and no line may remit more than is still outstanding on its collection across earlier remittances.
/// </summary>
public sealed class RecordCodRemittanceCommandHandler(
    IAggregateRootRepository<CodRemittanceAggregate> remittanceRepository,
    IAggregateRootRepository<PaymentAggregate> paymentRepository,
    IBillingFinder finder,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordCodRemittanceCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordCodRemittanceCommand command)
    {
        var courier = command.CourierCode.Trim();
        var remitted = await finder.GetRemittedTotalsAsync(command.Lines.Select(l => l.PaymentId).Distinct().ToList());

        foreach (var line in command.Lines)
        {
            var payment = await paymentRepository.GetByIdAsync(line.PaymentId);
            if (payment is null || payment.BranchId != command.BranchId)
                return Violation(BusinessViolationCodes.PaymentNotFound, nameof(command.Lines), "A collection was not found.");

            if (payment.Status != PaymentStatus.Accepted || !PaymentMethodRules.IsCashOnDelivery(payment.Method) || payment.Collector?.Kind != CollectorKind.Courier)
                return Violation(BusinessViolationCodes.CollectionNotCod, nameof(command.Lines), "Only cash collected by a courier on delivery can be remitted.");

            if (!string.Equals(payment.Collector.CourierCode, courier, StringComparison.OrdinalIgnoreCase))
                return Violation(BusinessViolationCodes.CollectionCourierMismatch, nameof(command.Lines), "A collection was made by a different courier.");

            var outstanding = CodShortfall.OutstandingOf(payment.Amount, remitted.GetValueOrDefault(payment.Id));
            if (line.AmountRemitted > outstanding)
                return Violation(BusinessViolationCodes.RemittanceExceedsCollection, nameof(command.Lines),
                    $"Only {BillingMessages.FormatMoney(outstanding, payment.CurrencyCode)} is still outstanding on a collection.");
        }

        var remittance = new CodRemittanceAggregate(command.RemittanceId);
        try
        {
            remittance.Record(command.BranchId, new CodRemittanceDto(
                courier, command.ReceivedOn, command.Amount, businessSettings.GetString(SettingKeys.CommerceCurrencyCode),
                BillingMapper.ParseEnum<RemittanceMethod>(command.Method), command.Reference,
                command.Lines.Select(l => new CodRemittanceLineInput(l.PaymentId, l.AmountRemitted)).ToList(), timeProvider.GetUtcNow(), command.ToBillingActor()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await remittanceRepository.SaveAsync(remittance, EventPublishOption.InMemoryPublishOnly);
        return Success(new RecordedRemittanceResult(remittance.Id, remittance.Unapplied));
    }
}

/// <summary>C23 -- the same file cannot be imported twice for a bank account; descriptions are masked by the aggregate (C-5).</summary>
public sealed class ImportBankStatementCommandHandler(
    IAggregateRootRepository<BankStatementAggregate> statementRepository,
    IBillingFinder finder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ImportBankStatementCommand>
{
    public async Task<CommandResponse> HandleAsync(ImportBankStatementCommand command)
    {
        var sha = command.SourceSha256.ToLowerInvariant();
        if (await finder.StatementExistsAsync(command.BankAccountLabel.Trim(), sha))
            return Violation(BusinessViolationCodes.StatementDuplicate, nameof(command.SourceSha256), BillingMessages.StatementDuplicate);

        var statement = new BankStatementAggregate(command.StatementId);
        try
        {
            statement.Import(command.BranchId, new BankStatementImportDto(
                command.BankAccountLabel, command.PeriodFrom, command.PeriodTo, command.SourceFileName, sha,
                command.Lines.Select(l => new BankStatementLineInput(
                    l.LineNumber, l.TransactionDate, l.Amount, BillingMapper.ParseEnum<BankEntryDirection>(l.Direction), l.Reference, l.Description, l.BankTransactionId)).ToList(),
                timeProvider.GetUtcNow(), command.ToBillingActor()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await statementRepository.SaveAsync(statement, EventPublishOption.InMemoryPublishOnly);
        return Success(new ImportedStatementResult(statement.Id, statement.Lines.Count));
    }
}

/// <summary>
/// C24 (ADR-189) -- statement credits without an active match against transfer payments received in the statement period (plus the
/// tolerance) and not matched yet. <see cref="BankMatchSuggester"/> (pure) returns only lines with exactly one candidate; each match is recorded alone
/// through <see cref="IBankMatchRecorder"/>, which skips what a concurrent run already took, so a re-run or a parallel run adds nothing twice.
/// </summary>
public sealed class AutoMatchBankStatementCommandHandler(
    IAggregateRootRepository<BankStatementAggregate> statementRepository,
    IBillingFinder finder,
    IBankMatchRecorder recorder,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AutoMatchBankStatementCommand>
{
    public async Task<CommandResponse> HandleAsync(AutoMatchBankStatementCommand command)
    {
        var statement = await statementRepository.GetByIdAsync(command.StatementId);
        if (statement is null || statement.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.StatementNotFound, nameof(command.StatementId), "Statement not found.");

        var tolerance = businessSettings.GetInt(SettingKeys.BillingBankMatchDateToleranceDays, command.BranchId);
        var alreadyMatched = await finder.GetActivelyMatchedLineIdsAsync(statement.Id);
        var lines = statement.Lines
            .Where(l => l.Direction == BankEntryDirection.Credit && !alreadyMatched.Contains(l.Id))
            .Select(l => new BankLineCandidateInput(l.Id, l.TransactionDate, l.Amount, l.Reference, l.Description))
            .ToList();

        var candidates = await finder.FindUnmatchedTransferPaymentsAsync(statement.BranchId, statement.PeriodFrom.AddDays(-tolerance), statement.PeriodTo.AddDays(tolerance));
        var suggestions = BankMatchSuggester.Suggest(lines, candidates, tolerance);

        var now = timeProvider.GetUtcNow();
        var actor = command.ToBillingActor();
        var matched = 0;
        foreach (var suggestion in suggestions.Where(s => s.Outcome == BankMatchOutcomeKind.Matched))
        {
            var match = new BankMatchAggregate(Guid.NewGuid());
            match.Match(new BankMatchDto(statement.Id, suggestion.LineId, suggestion.PaymentId!.Value, BankMatchMethod.Auto, now, actor));

            if (await recorder.TryRecordAsync(match)) matched++;
        }

        return Success(new AutoMatchResult(
            matched, suggestions.Count(s => s.Outcome == BankMatchOutcomeKind.Ambiguous), suggestions.Count(s => s.Outcome == BankMatchOutcomeKind.None)));
    }
}

/// <summary>C25 -- a person matches one credit to one transfer payment: the line is a credit, the payment an accepted transfer, the amounts equal.</summary>
public sealed class MatchBankLineCommandHandler(
    IAggregateRootRepository<BankStatementAggregate> statementRepository,
    IAggregateRootRepository<PaymentAggregate> paymentRepository,
    IBillingFinder finder,
    IBankMatchRecorder recorder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<MatchBankLineCommand>
{
    public async Task<CommandResponse> HandleAsync(MatchBankLineCommand command)
    {
        var statement = await statementRepository.GetByIdAsync(command.StatementId);
        if (statement is null || statement.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.StatementNotFound, nameof(command.StatementId), "Statement not found.");

        var line = statement.Lines.FirstOrDefault(l => l.Id == command.StatementLineId);
        if (line is null || line.Direction != BankEntryDirection.Credit)
            return Violation(BusinessViolationCodes.StatementLineNotFound, nameof(command.StatementLineId), "Only a credit line of this statement can be matched.");

        var payment = await paymentRepository.GetByIdAsync(command.PaymentId);
        if (payment is null || payment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.PaymentNotFound, nameof(command.PaymentId), "Payment not found.");

        if (payment.Status != PaymentStatus.Accepted || !PaymentMethodRules.IsBankTransfer(payment.Method))
            return Violation(BusinessViolationCodes.MatchMethodNotTransfer, nameof(command.PaymentId), "Only an accepted bank-transfer payment can be matched to a bank line.");

        if (payment.Amount != line.Amount)
            return Violation(BusinessViolationCodes.MatchAmountMismatch, nameof(command.PaymentId),
                $"The bank line is {BillingMessages.FormatMoney(line.Amount, payment.CurrencyCode)} but the payment is {BillingMessages.FormatMoney(payment.Amount, payment.CurrencyCode)}.");

        if ((await finder.GetActivelyMatchedLineIdsAsync(statement.Id)).Contains(line.Id))
            return Violation(BusinessViolationCodes.BankLineAlreadyMatched, nameof(command.StatementLineId), BillingMessages.BankLineAlreadyMatched);

        if (await finder.IsPaymentActivelyMatchedAsync(payment.Id))
            return Violation(BusinessViolationCodes.PaymentAlreadyMatched, nameof(command.PaymentId), BillingMessages.PaymentAlreadyMatched);

        var match = new BankMatchAggregate(command.MatchId);
        match.Match(new BankMatchDto(statement.Id, line.Id, payment.Id, BankMatchMethod.Manual, timeProvider.GetUtcNow(), command.ToBillingActor()));

        if (!await recorder.TryRecordAsync(match))
            return await finder.IsPaymentActivelyMatchedAsync(payment.Id)
                ? Violation(BusinessViolationCodes.PaymentAlreadyMatched, nameof(command.PaymentId), BillingMessages.PaymentAlreadyMatched)
                : Violation(BusinessViolationCodes.BankLineAlreadyMatched, nameof(command.StatementLineId), BillingMessages.BankLineAlreadyMatched);

        return Success(match.Id);
    }
}

/// <summary>C25 -- undo a match; the row stays with who, when and why (the reason is audited).</summary>
public sealed class UnmatchBankLineCommandHandler(
    IAggregateRootRepository<BankMatchAggregate> matchRepository,
    IAggregateRootRepository<BankStatementAggregate> statementRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<UnmatchBankLineCommand>
{
    public async Task<CommandResponse> HandleAsync(UnmatchBankLineCommand command)
    {
        var match = await matchRepository.GetByIdAsync(command.MatchId);
        var statement = match is null ? null : await statementRepository.GetByIdAsync(match.StatementId);
        if (match is null || statement is null || statement.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.BankMatchNotFound, nameof(command.MatchId), "Match not found.");

        try
        {
            match.Unmatch(command.Reason, timeProvider.GetUtcNow(), command.ToBillingActor());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await matchRepository.UpdateAsync(match, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

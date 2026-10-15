using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Billing;

/// <summary>The courier-collected COD payments of a branch with what has been remitted against each -- one query behind the collections list
/// and the per-courier reconciliation, so the two cannot disagree.</summary>
internal static class CodCollectionRows
{
    public sealed class Row
    {
        public Guid PaymentId { get; init; }
        public long? ReceiptNumber { get; init; }
        public Guid CustomerId { get; init; }
        public string CustomerName { get; init; } = string.Empty;
        public string? CourierCode { get; init; }
        public string? CourierName { get; init; }
        public DateOnly ReceivedOn { get; init; }
        public decimal Amount { get; init; }
        public string CurrencyCode { get; init; } = string.Empty;
        public decimal Remitted { get; init; }
    }

    public static IQueryable<Row> Query(CatEcommerceDbContext db, Guid branchId, string? courierCode, DateOnly? from, DateOnly? to)
    {
        var payments = db.Set<PaymentAggregate>().AsNoTracking()
            .Where(p => p.BranchId == branchId && p.Status == PaymentStatus.Accepted && p.Method == PaymentMethodRules.CashOnDeliveryMethod
                        && p.Collector != null && p.Collector.Kind == CollectorKind.Courier);

        if (!string.IsNullOrWhiteSpace(courierCode))
        {
            var code = courierCode.Trim().ToUpper();
            payments = payments.Where(p => p.Collector!.CourierCode != null && p.Collector.CourierCode.ToUpper() == code);
        }

        if (from is { } rangeFrom) payments = payments.Where(p => p.ReceivedOn >= rangeFrom);
        if (to is { } rangeTo) payments = payments.Where(p => p.ReceivedOn <= rangeTo);

        return
            from p in payments
            join customer in db.Set<CustomerAccountAggregate>().AsNoTracking() on p.CustomerId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            select new Row
            {
                PaymentId = p.Id, ReceiptNumber = p.ReceiptNumber, CustomerId = p.CustomerId, CustomerName = customer != null ? customer.FullName : string.Empty,
                CourierCode = p.Collector!.CourierCode, CourierName = p.Collector.CourierName, ReceivedOn = p.ReceivedOn, Amount = p.Amount, CurrencyCode = p.CurrencyCode,
                Remitted = db.Set<CodRemittanceAggregate>().SelectMany(r => r.Lines).Where(l => l.PaymentId == p.Id).Sum(l => (decimal?)l.AmountRemitted) ?? 0m,
            };
    }

    public static CodCollectionViewModel ToView(Row row, DateOnly today, int dueDays) => new(
        row.PaymentId, PaymentReadSupport.Receipt(row.ReceiptNumber), row.CustomerId, row.CustomerName, row.CourierCode, row.CourierName, row.ReceivedOn,
        BillingMoney.Of(row.Amount, row.CurrencyCode), BillingMoney.Of(row.Remitted, row.CurrencyCode),
        BillingMoney.Of(CodShortfall.OutstandingOf(row.Amount, row.Remitted), row.CurrencyCode), BillingMoney.Of(CodShortfall.ShortfallOf(row.Amount, row.Remitted), row.CurrencyCode),
        CodShortfall.StatusOf(row.Amount, row.Remitted).ToString(), CodShortfall.IsOverdue(row.ReceivedOn, today, dueDays, row.Amount, row.Remitted));

    public static async Task<DateOnly> TodayAsync(CatEcommerceDbContext db, Guid branchId, TimeProvider timeProvider)
    {
        var zoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == branchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? BillingRules.DefaultTimeZoneId;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(zoneId)).DateTime);
    }
}

public sealed class GetCodCollectionsQueryHandler(CatEcommerceDbContext db, IBusinessSettings settings, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetCodCollectionsQuery, PagedResult<CodCollectionViewModel>>
{
    public async Task<QueryResponse<PagedResult<CodCollectionViewModel>>> HandleAsync(GetCodCollectionsQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);
        var pageNumber = Math.Max(query.PageNumber, 1);

        var rows = CodCollectionRows.Query(db, query.BranchId, query.CourierCode, query.From, query.To);
        rows = query.Status?.Trim().ToLowerInvariant() switch
        {
            "unremitted" => rows.Where(r => r.Remitted <= 0m),
            "shortfall" => rows.Where(r => r.Remitted > 0m && r.Remitted < r.Amount),
            "remitted" => rows.Where(r => r.Remitted >= r.Amount),
            _ => rows,
        };

        var total = await rows.CountAsync();
        var page = await rows.OrderBy(r => r.ReceivedOn).ThenBy(r => r.PaymentId).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        var today = await CodCollectionRows.TodayAsync(db, query.BranchId, timeProvider);
        var dueDays = settings.GetInt(SettingKeys.BillingCodRemittanceDueDays, query.BranchId);
        var items = page.Select(r => CodCollectionRows.ToView(r, today, dueDays)).ToList();

        return new QueryResponse<PagedResult<CodCollectionViewModel>> { Result = new PagedResult<CodCollectionViewModel>(items, pageNumber, pageSize, total) };
    }
}

public sealed class GetCodRemittancesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCodRemittancesQuery, PagedResult<CodRemittanceViewModel>>
{
    public async Task<QueryResponse<PagedResult<CodRemittanceViewModel>>> HandleAsync(GetCodRemittancesQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);
        var pageNumber = Math.Max(query.PageNumber, 1);

        var remittances = db.Set<CodRemittanceAggregate>().AsNoTracking().Where(r => r.BranchId == query.BranchId);
        if (!string.IsNullOrWhiteSpace(query.CourierCode))
        {
            var code = query.CourierCode.Trim().ToUpper();
            remittances = remittances.Where(r => r.CourierCode.ToUpper() == code);
        }

        var total = await remittances.CountAsync();
        var rows = await (
            from r in remittances.OrderByDescending(r => r.RecordedOn).ThenBy(r => r.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            join staff in db.Set<StaffAccountAggregate>().AsNoTracking() on r.RecordedBy equals staff.Id into staffRows
            from staff in staffRows.DefaultIfEmpty()
            select new
            {
                r.Id, r.CourierCode, r.ReceivedOn, r.Amount, r.CurrencyCode, r.Method, r.Reference, r.RecordedBy, r.RecordedOn,
                Applied = r.Lines.Sum(l => l.AmountRemitted), LineCount = r.Lines.Count, StaffName = staff != null ? staff.FullName : string.Empty,
            }).ToListAsync();

        var items = rows.Select(r => new CodRemittanceViewModel(
            r.Id, r.CourierCode, r.ReceivedOn, BillingMoney.Of(r.Amount, r.CurrencyCode), r.Method.DisplayName, r.Reference, BillingMoney.Of(r.Applied, r.CurrencyCode),
            BillingMoney.Of(r.Amount - r.Applied, r.CurrencyCode), r.LineCount, r.RecordedBy, r.StaffName, r.RecordedOn)).ToList();

        return new QueryResponse<PagedResult<CodRemittanceViewModel>> { Result = new PagedResult<CodRemittanceViewModel>(items, pageNumber, pageSize, total) };
    }
}

/// <summary>Per courier: collected, remitted, the shortfall (part-remitted collections), what has not been remitted at all, and money received
/// that no line covers -- US-BIL-06 AC1.</summary>
public sealed class GetCodReconciliationQueryHandler(CatEcommerceDbContext db, IBusinessSettings settings, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetCodReconciliationQuery, CodReconciliationViewModel>
{
    public async Task<QueryResponse<CodReconciliationViewModel>> HandleAsync(GetCodReconciliationQuery query)
    {
        var currency = settings.GetString(SettingKeys.CommerceCurrencyCode);
        var today = await CodCollectionRows.TodayAsync(db, query.BranchId, timeProvider);
        var dueDays = settings.GetInt(SettingKeys.BillingCodRemittanceDueDays, query.BranchId);

        var collections = await CodCollectionRows.Query(db, query.BranchId, query.CourierCode, query.From, query.To)
            .OrderBy(r => r.ReceivedOn).ThenBy(r => r.PaymentId).Take(BillingRules.MaxExportRows).ToListAsync();

        var remittances = db.Set<CodRemittanceAggregate>().AsNoTracking().Where(r => r.BranchId == query.BranchId);
        if (!string.IsNullOrWhiteSpace(query.CourierCode))
        {
            var code = query.CourierCode.Trim().ToUpper();
            remittances = remittances.Where(r => r.CourierCode.ToUpper() == code);
        }

        if (query.From is { } from) remittances = remittances.Where(r => r.ReceivedOn >= from);
        if (query.To is { } to) remittances = remittances.Where(r => r.ReceivedOn <= to);

        var unapplied = (await remittances.Select(r => new { r.CourierCode, Unapplied = r.Amount - r.Lines.Sum(l => l.AmountRemitted) }).ToListAsync())
            .GroupBy(r => r.CourierCode.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.Sum(r => r.Unapplied));

        var couriers = collections.GroupBy(c => (c.CourierCode ?? string.Empty).ToUpperInvariant()).Select(g => g.Key)
            .Union(unapplied.Keys).OrderBy(k => k, StringComparer.Ordinal).Select(key =>
            {
                var rows = collections.Where(c => (c.CourierCode ?? string.Empty).ToUpperInvariant() == key).ToList();
                var collected = rows.Sum(r => r.Amount);
                var remitted = rows.Sum(r => r.Remitted);
                var shortfallRows = rows.Where(r => CodShortfall.StatusOf(r.Amount, r.Remitted) == CodCollectionStatus.Shortfall).ToList();

                return new CodCourierReconciliationViewModel(
                    rows.FirstOrDefault()?.CourierCode ?? key, BillingMoney.Of(collected, currency), BillingMoney.Of(remitted, currency),
                    BillingMoney.Of(shortfallRows.Sum(r => CodShortfall.ShortfallOf(r.Amount, r.Remitted)), currency),
                    BillingMoney.Of(rows.Where(r => r.Remitted <= 0m).Sum(r => r.Amount), currency), BillingMoney.Of(unapplied.GetValueOrDefault(key), currency),
                    shortfallRows.Select(r => CodCollectionRows.ToView(r, today, dueDays)).ToList());
            }).ToList();

        return new QueryResponse<CodReconciliationViewModel> { Result = new CodReconciliationViewModel(couriers) };
    }
}

public sealed class GetBankStatementsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetBankStatementsQuery, PagedResult<BankStatementSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<BankStatementSummaryViewModel>>> HandleAsync(GetBankStatementsQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);
        var pageNumber = Math.Max(query.PageNumber, 1);

        var statements = db.Set<BankStatementAggregate>().AsNoTracking().Where(s => s.BranchId == query.BranchId);
        var total = await statements.CountAsync();
        var rows = await (
            from s in statements.OrderByDescending(s => s.ImportedOn).ThenBy(s => s.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            join staff in db.Set<StaffAccountAggregate>().AsNoTracking() on s.ImportedBy equals staff.Id into staffRows
            from staff in staffRows.DefaultIfEmpty()
            select new
            {
                s.Id, s.BankAccountLabel, s.PeriodFrom, s.PeriodTo, s.SourceFileName, LineCount = s.Lines.Count, s.ImportedOn, s.ImportedBy,
                StaffName = staff != null ? staff.FullName : string.Empty,
                MatchedCount = db.Set<BankMatchAggregate>().Count(m => m.StatementId == s.Id && m.UnmatchedOn == null),
            }).ToListAsync();

        var items = rows.Select(r => new BankStatementSummaryViewModel(
            r.Id, r.BankAccountLabel, r.PeriodFrom, r.PeriodTo, r.SourceFileName, r.LineCount, r.MatchedCount, r.ImportedOn, r.ImportedBy, r.StaffName)).ToList();

        return new QueryResponse<PagedResult<BankStatementSummaryViewModel>> { Result = new PagedResult<BankStatementSummaryViewModel>(items, pageNumber, pageSize, total) };
    }
}

/// <summary>US-BIL-06 AC2 -- flagged both ways: statement credits no payment explains, and transfer payments no statement credit explains.</summary>
public sealed class GetBankStatementReconciliationQueryHandler(CatEcommerceDbContext db, IBusinessSettings settings)
    : IQueryHandlerAsync<GetBankStatementReconciliationQuery, BankStatementReconciliationViewModel?>
{
    public async Task<QueryResponse<BankStatementReconciliationViewModel?>> HandleAsync(GetBankStatementReconciliationQuery query)
    {
        var statement = await db.Set<BankStatementAggregate>().AsNoTracking().Where(s => s.Id == query.StatementId && s.BranchId == query.BranchId)
            .Select(s => new { s.Id, s.BranchId, s.BankAccountLabel, s.PeriodFrom, s.PeriodTo, s.SourceFileName, s.ImportedOn, s.ImportedBy, LineCount = s.Lines.Count })
            .FirstOrDefaultAsync();
        if (statement is null) return new QueryResponse<BankStatementReconciliationViewModel?> { Result = null };

        var currency = settings.GetString(SettingKeys.CommerceCurrencyCode);
        var tolerance = settings.GetInt(SettingKeys.BillingBankMatchDateToleranceDays, query.BranchId);
        var staffName = await db.Set<StaffAccountAggregate>().AsNoTracking().Where(s => s.Id == statement.ImportedBy).Select(s => s.FullName).FirstOrDefaultAsync() ?? string.Empty;

        var matches = await db.Set<BankMatchAggregate>().AsNoTracking().Where(m => m.StatementId == statement.Id).OrderBy(m => m.MatchedOn).ThenBy(m => m.Id)
            .Select(m => new { m.Id, m.StatementLineId, m.PaymentId, m.Method, m.MatchedOn, m.MatchedBy, m.UnmatchedOn, m.UnmatchReason }).ToListAsync();
        var matchedPayments = matches.Select(m => m.PaymentId).Distinct().ToList();
        var paymentInfo = (await db.Set<PaymentAggregate>().AsNoTracking().Where(p => matchedPayments.Contains(p.Id))
            .Select(p => new { p.Id, p.ReceiptNumber, p.Amount }).ToListAsync()).ToDictionary(p => p.Id);

        var activeLineIds = matches.Where(m => m.UnmatchedOn is null).Select(m => m.StatementLineId).ToHashSet();

        var lineRows = await db.Set<BankStatementAggregate>().AsNoTracking().Where(s => s.Id == statement.Id).SelectMany(s => s.Lines).OrderBy(l => l.LineNumber)
            .Select(l => new { l.Id, l.LineNumber, l.TransactionDate, l.Amount, l.Direction, l.Reference, l.Description }).ToListAsync();
        var credits = lineRows.Where(l => l.Direction == BankEntryDirection.Credit).ToList();
        var unmatchedLines = credits.Where(l => !activeLineIds.Contains(l.Id))
            .Select(l => new BankLineViewModel(l.Id, l.LineNumber, l.TransactionDate, BillingMoney.Of(l.Amount, currency), l.Direction.DisplayName, l.Reference, l.Description)).ToList();

        var windowFrom = statement.PeriodFrom.AddDays(-tolerance);
        var windowTo = statement.PeriodTo.AddDays(tolerance);
        var unmatchedPayments = await (
            from p in db.Set<PaymentAggregate>().AsNoTracking()
            where p.BranchId == statement.BranchId && p.Status == PaymentStatus.Accepted && p.Method == PaymentMethodRules.BankTransferMethod && p.ReceivedOn >= windowFrom && p.ReceivedOn <= windowTo
                  && !db.Set<BankMatchAggregate>().Any(m => m.PaymentId == p.Id && m.UnmatchedOn == null)
            join customer in db.Set<CustomerAccountAggregate>().AsNoTracking() on p.CustomerId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            orderby p.ReceivedOn, p.Id
            select new { p.Id, p.ReceiptNumber, p.CustomerId, CustomerName = customer != null ? customer.FullName : string.Empty, p.Amount, p.CurrencyCode, p.ReceivedOn, p.ExternalReference }).ToListAsync();

        var summary = new BankStatementSummaryViewModel(
            statement.Id, statement.BankAccountLabel, statement.PeriodFrom, statement.PeriodTo, statement.SourceFileName, statement.LineCount,
            matches.Count(m => m.UnmatchedOn is null), statement.ImportedOn, statement.ImportedBy, staffName);

        var result = new BankStatementReconciliationViewModel(
            summary, unmatchedLines,
            unmatchedPayments.Select(p => new UnmatchedPaymentViewModel(p.Id, PaymentReadSupport.Receipt(p.ReceiptNumber), p.CustomerId, p.CustomerName, BillingMoney.Of(p.Amount, p.CurrencyCode), p.ReceivedOn, p.ExternalReference)).ToList(),
            matches.Select(m => new BankMatchViewModel(
                m.Id, m.StatementLineId, m.PaymentId, paymentInfo.TryGetValue(m.PaymentId, out var info) ? PaymentReadSupport.Receipt(info.ReceiptNumber) : null,
                BillingMoney.Of(paymentInfo.TryGetValue(m.PaymentId, out var amountInfo) ? amountInfo.Amount : 0m, currency), m.Method.DisplayName, m.MatchedOn, m.MatchedBy, m.UnmatchedOn, m.UnmatchReason)).ToList(),
            statement.LineCount - credits.Count);

        return new QueryResponse<BankStatementReconciliationViewModel?> { Result = result };
    }
}

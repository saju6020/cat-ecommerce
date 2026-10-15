using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Billing;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Billing;

/// <summary>
/// HO-BIL-001 section 12.4 -- the cash-up of one day. An open day's expected totals are the calculator's over the live rows; a closed
/// day shows the frozen snapshot (the rows are still listed, so the variance can be traced).
/// </summary>
public sealed class GetCashUpQueryHandler(CatEcommerceDbContext db, IBusinessSettings settings) : IQueryHandlerAsync<GetCashUpQuery, CashUpViewModel>
{
    public async Task<QueryResponse<CashUpViewModel>> HandleAsync(GetCashUpQuery query)
    {
        var currency = settings.GetString(SettingKeys.CommerceCurrencyCode);
        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";

        var cashUp = await db.Set<CashUpAggregate>().AsNoTracking().Where(c => c.BranchId == query.BranchId && c.BusinessDate == query.BusinessDate)
            .Select(c => new { c.Version, c.Status, c.CountedCash, c.CountedBy, c.CountedOn, c.Variance, c.Explanation, c.ClosedBy, c.ClosedOn, c.ExpectedSnapshot })
            .FirstOrDefaultAsync();
        var rows = await CashUpRows.LoadAsync(db, query.BranchId, query.BusinessDate);
        var pending = await CashUpRows.CountPendingApprovalsAsync(db, query.BranchId, query.BusinessDate, timeZoneId);

        var closed = cashUp?.Status == CashUpStatus.Closed && cashUp.ExpectedSnapshot is not null;
        var snapshot = closed ? cashUp!.ExpectedSnapshot! : CashUpCalculator.Calculate(rows);
        var money = (decimal amount) => BillingMoney.Of(amount, currency);

        var carried = rows.Where(r => r.CarriedIn).Select(r => new CashUpCarriedInViewModel(r.Id, r.Kind.ToString(), r.Label, money(SignedCash(r)))).ToList();
        var expected = new CashUpExpectedViewModel(
            money(snapshot.CashTotal), snapshot.CashCount, money(snapshot.CardTotal), snapshot.CardCount, money(snapshot.TransferTotal), snapshot.TransferCount,
            money(snapshot.CashRefunds), money(snapshot.CashReversals), money(snapshot.CarriedIn), carried, money(snapshot.ExpectedCash));

        var lines = rows.Select(r => new CashUpLineViewModel(
            r.Id, r.Kind.ToString(), r.Method?.DisplayName, r.Collector?.DisplayName, r.RefundMethod?.DisplayName, money(r.Amount), SignedCash(r) != 0m, r.CarriedIn, r.Label)).ToList();

        decimal? variance = cashUp is null ? null : cashUp.Variance ?? (cashUp.CountedCash is { } counted ? counted - snapshot.ExpectedCash : null);
        var closer = cashUp?.ClosedBy is { } closedBy
            ? await db.Set<CatEcommerce.Domain.Aggregates.Account.StaffAccountAggregate>().AsNoTracking().Where(s => s.Id == closedBy).Select(s => s.FullName).FirstOrDefaultAsync()
            : null;

        return new QueryResponse<CashUpViewModel>
        {
            Result = new CashUpViewModel(
                query.BusinessDate, (cashUp?.Status ?? CashUpStatus.Open).DisplayName, cashUp?.Version ?? 0, expected, lines,
                cashUp?.CountedCash is { } c ? money(c) : null, variance is { } v ? money(v) : null, cashUp?.Explanation, pending,
                cashUp?.CountedBy, cashUp?.CountedOn, cashUp?.ClosedBy, closer, cashUp?.ClosedOn),
        };
    }

    /// <summary>The effect of a row on the till drawer (zero for card, transfer and courier-held cash).</summary>
    private static decimal SignedCash(CashUpRow row) => row.Kind switch
    {
        CashUpRowKind.Payment => PaymentMethodRules.CountsInTill(row.Method!, row.Collector) ? row.Amount : 0m,
        CashUpRowKind.Refund => row.RefundMethod == RefundMethod.CashInStore ? -row.Amount : 0m,
        _ => PaymentMethodRules.CountsInTill(row.Method!, row.Collector) ? -row.Amount : 0m,
    };
}

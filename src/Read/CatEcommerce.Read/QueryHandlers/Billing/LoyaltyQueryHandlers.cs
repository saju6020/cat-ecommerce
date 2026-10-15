using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Billing;

/// <summary>HO-BIL-001 section 8.11 -- a customer points balance and the ledger behind it, newest first. Works whether or not loyalty is switched on
/// (a ledger that exists stays readable); <c>Enabled</c> tells the screen whether to offer redemption.</summary>
public sealed class GetLoyaltyQueryHandler(CatEcommerceDbContext db, IBusinessSettings settings) : IQueryHandlerAsync<GetLoyaltyQuery, LoyaltyViewModel>
{
    public async Task<QueryResponse<LoyaltyViewModel>> HandleAsync(GetLoyaltyQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);
        var pageNumber = Math.Max(query.PageNumber, 1);

        var balance = await LoyaltyBalanceLookup.BalanceAsync(db, query.CustomerId);
        var entries = db.Set<LoyaltyEntryAggregate>().AsNoTracking().Where(e => e.CustomerId == query.CustomerId);
        var total = await entries.CountAsync();

        var rows = await (
            from e in entries.OrderByDescending(e => e.OccurredOn).ThenBy(e => e.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            join c in db.Set<CreditNoteAggregate>().AsNoTracking() on e.CreditNoteId equals c.Id into notes
            from c in notes.DefaultIfEmpty()
            let invoiceKey = e.InvoiceId ?? (c != null ? c.InvoiceId : (Guid?)null)
            join i in db.Set<InvoiceAggregate>().AsNoTracking() on invoiceKey equals i.Id into invoices
            from i in invoices.DefaultIfEmpty()
            select new { e.Id, e.Kind, e.Points, InvoiceId = invoiceKey, InvoiceNumber = i != null ? i.Number : null, e.CreditNoteId, e.Shortfall, e.OccurredOn }).ToListAsync();

        var items = rows.Select(r => new LoyaltyEntryViewModel(r.Id, r.Kind.DisplayName, r.Points, r.InvoiceId, r.InvoiceNumber, r.CreditNoteId, r.Shortfall, r.OccurredOn)).ToList();
        return new QueryResponse<LoyaltyViewModel>
        {
            Result = new LoyaltyViewModel(settings.GetBool(SettingKeys.BillingLoyaltyEnabled), balance, new PagedResult<LoyaltyEntryViewModel>(items, pageNumber, pageSize, total)),
        };
    }
}

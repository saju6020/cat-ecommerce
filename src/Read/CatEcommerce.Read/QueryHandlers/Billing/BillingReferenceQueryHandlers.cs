using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Billing;

public sealed class GetPaymentMethodsQueryHandler : IQueryHandlerAsync<GetPaymentMethodsQuery, IReadOnlyList<PaymentMethodViewModel>>
{
    public Task<QueryResponse<IReadOnlyList<PaymentMethodViewModel>>> HandleAsync(GetPaymentMethodsQuery query)
    {
        IReadOnlyList<PaymentMethodViewModel> methods = Enumeration.GetAll<PaymentMethod>()
            .OrderBy(m => m.Value)
            .Select(m => new PaymentMethodViewModel(
                m.Value, m.DisplayName, m.CountsInTill || m.CountsInTillWhenStaffCollected, m.RequiresReference, m.ReferenceLabel,
                m.RequiresCollector, m.IsOnline, m.AllowedRefundMethods.Select(r => r.DisplayName).ToList()))
            .ToList();

        return Task.FromResult(new QueryResponse<IReadOnlyList<PaymentMethodViewModel>> { Result = methods });
    }
}

public sealed class GetBillingProfileQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetBillingProfileQuery, BillingProfileViewModel?>
{
    public async Task<QueryResponse<BillingProfileViewModel?>> HandleAsync(GetBillingProfileQuery query)
    {
        var profile = await db.Set<BillingProfileAggregate>().AsNoTracking()
            .Where(p => p.Id == query.BranchId)
            .Select(p => new
            {
                p.Id, p.LegalName, p.TaxRegistrationNumber, p.AddressLines, p.Phone, p.Email, p.InvoiceFooter, p.Version,
                Accounts = p.BankAccounts.OrderBy(a => a.SortOrder).ToList(),
            })
            .FirstOrDefaultAsync();

        BillingProfileViewModel? result = profile is null ? null : new BillingProfileViewModel(
            profile.Id, profile.LegalName, profile.TaxRegistrationNumber, profile.AddressLines.ToList(), profile.Phone, profile.Email,
            profile.InvoiceFooter,
            profile.Accounts.Select(a => new BillingBankAccountViewModel(a.BankName, a.AccountName, a.AccountNumber, a.BranchName, a.RoutingNumber)).ToList(),
            profile.Version);

        return new QueryResponse<BillingProfileViewModel?> { Result = result };
    }
}

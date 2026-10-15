using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Account;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Ordering;

/// <summary>
/// HO-ORD-001 ADR-216 (amended FU1 D1, 2026-10-10) -- <see cref="ICounterCustomerFinder"/> over HO-ACC-001's published <see cref="FindCustomersQuery"/> and
/// <see cref="GetStaffCustomerQuery"/> (dispatched in-process, as the caller). A customer whose <c>MobileMatchedOn</c> is <c>Primary</c> and who is <c>Active</c> is
/// the buyer. A <c>Merged</c> holder of the number is followed through <c>MergedIntoCustomerId</c> (chains included, bounded) to the surviving account, which is the
/// buyer when it is <c>Active</c>. A survivor that is not active (or a broken chain) yields <c>null</c>: the mobile is then "held by an inactive account".
/// </summary>
public sealed class AccountsCounterCustomerFinder(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ICounterCustomerFinder
{
    private const int MaxMergeHops = 10;

    public async Task<CustomerContactFacts?> FindActiveByPrimaryMobileAsync(string normalisedMobile, CancellationToken ct)
    {
        var response = await dispatcher.GetResultAsync(new FindCustomersQuery
        {
            Mobile = normalisedMobile, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        var primary = response.Result?.Where(c => c.MobileMatchedOn == "Primary").ToList();
        if (primary is null || primary.Count == 0) return null;

        if (primary.FirstOrDefault(c => c.Status == "Active") is { } active)
            return new CustomerContactFacts(active.Id, active.FullName, active.MobileNumber, IsActive: true);

        return primary.FirstOrDefault(c => c.Status == "Merged") is { } merged ? await FollowMergeAsync(merged.Id) : null;
    }

    private async Task<CustomerContactFacts?> FollowMergeAsync(Guid mergedCustomerId)
    {
        var current = mergedCustomerId;
        var seen = new HashSet<Guid> { current };
        for (var hop = 0; hop < MaxMergeHops; hop++)
        {
            var read = await dispatcher.GetResultAsync(new GetStaffCustomerQuery
            {
                CustomerId = current, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
            });

            var account = read.Result;
            if (account is null) return null;
            if (account.Status == "Active") return new CustomerContactFacts(account.Id, account.FullName, account.MobileNumber, IsActive: true);
            if (account.Status != "Merged" || account.MergedIntoCustomerId is not { } next || !seen.Add(next)) return null;
            current = next;
        }

        return null;
    }
}

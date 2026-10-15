using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Portal.Sections;

/// <summary>HO-ACC-001 Q-C2 (HO-POR-001 §8.3): <c>fullName</c> only -- a greeting. No mobile, email or addresses.</summary>
public sealed class ProfileReader(IQueryHandlerAsync<GetCustomerProfileQuery, CustomerProfileViewModel?> profileHandler)
    : IPortalSectionReader<PortalCustomerViewModel>
{
    public async Task<PortalCustomerViewModel> ReadAsync(PortalCaller caller)
    {
        var profile = (await profileHandler.HandleAsync(new GetCustomerProfileQuery
        {
            CustomerId = caller.CustomerId, UserContext = caller.UserContext, CorrelationId = caller.CorrelationId,
        })).Result;

        // A customer session always has an account; a missing one is an upstream fault, not an empty greeting.
        return profile is null
            ? throw new InvalidOperationException("The signed-in customer profile was not found.")
            : new PortalCustomerViewModel(profile.FullName);
    }
}

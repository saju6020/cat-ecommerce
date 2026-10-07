using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Account;

/// <summary>Q-F1. Code-defined (ADR-036) -- no database access.</summary>
public sealed class GetStaffRoleCatalogueQueryHandler : IQueryHandlerAsync<GetStaffRoleCatalogueQuery, StaffRoleCatalogueViewModel>
{
    public Task<QueryResponse<StaffRoleCatalogueViewModel>> HandleAsync(GetStaffRoleCatalogueQuery query)
    {
        var cells = StaffRoleCatalogue.Cells
            .Select(c => new StaffCapabilityCellViewModel(c.Capability, c.RoleId, c.Access.DisplayName, c.Qualifier))
            .ToList();

        var viewModel = new StaffRoleCatalogueViewModel(StaffRoles.All, StaffRoleCatalogue.Capabilities, cells);

        return Task.FromResult(new QueryResponse<StaffRoleCatalogueViewModel> { Result = viewModel });
    }
}

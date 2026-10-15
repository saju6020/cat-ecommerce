using CatEcommerce.Read.Queries.Clinic;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Portal.Sections;

/// <summary>HO-APT-001 <c>GetCustomerAppointmentsQuery</c> (HO-POR-001 §8.3): scope <c>upcoming</c>, page 1, <c>DashboardItemLimit</c>.
/// The upstream handler filters by the customer id passed here, which is the caller's own.</summary>
public sealed class AppointmentsSectionReader(
    IQueryHandlerAsync<GetCustomerAppointmentsQuery, PagedResult<AppointmentSummaryViewModel>> appointmentsHandler)
    : IPortalSectionReader<PortalAppointmentsViewModel>
{
    public async Task<PortalAppointmentsViewModel> ReadAsync(PortalCaller caller)
    {
        var response = await appointmentsHandler.HandleAsync(new GetCustomerAppointmentsQuery
        {
            CustomerId = caller.CustomerId,
            Scope = "upcoming",
            PageNumber = 1,
            PageSize = PortalRules.DashboardItemLimit,
            UserContext = caller.UserContext,
            CorrelationId = caller.CorrelationId,
        });

        var items = response.Result.Items
            .Take(PortalRules.DashboardItemLimit)
            .Select(a => new PortalAppointmentItemViewModel(
                a.Id, a.CatId, a.CatName, a.Service.Name, a.StartsOn, a.EndsOn, a.PerformerName,
                a.Branch.Name, a.BranchTimeZoneId, a.Status))
            .ToList();

        return new PortalAppointmentsViewModel(items);
    }
}

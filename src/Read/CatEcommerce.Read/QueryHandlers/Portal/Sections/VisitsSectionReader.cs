using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Portal.Sections;

/// <summary>
/// HO-EMR-001 (HO-POR-001 §8.3, §8.6): the LATEST FINALISED visit per alive cat -- identifying metadata only (ADR-262). Nothing clinical
/// is carried: the upstream list's problem names are read and discarded, and no diagnosis, note or lab value exists in the output type.
/// The follow-up DATE (<c>followUpOn</c>) is shown -- EMR added it to its list item on the owner's decision of 2026-10-10 (ADR-262 amended).
///
/// Reconciled with the real contract (HO-POR-001 §1 Amended 2026-10-09): EMR publishes a PER-CAT list
/// (<c>GetCustomerCatVisitsQuery { CatId, CustomerId }</c>), not the per-customer query §10.2 assumed, and it returns finalised visits only
/// (it has no <c>isFinalised</c> flag to filter again and no catName; its list item has carried <c>followUpOn</c> since 2026-10-10). The reader therefore asks for the caller's alive cats
/// through the Cat Profiles handler -- the same owner-filtered call the cats section makes, so every cat id comes from an owner-filtered
/// result in this request (ADR-261) -- then asks EMR for each, one after another (F36). EMR re-checks current ownership itself and answers
/// <c>null</c> for a cat that is not the customer's, which is skipped.
/// </summary>
public sealed class VisitsSectionReader(
    IQueryHandlerAsync<GetCatsQuery, PagedResult<CatListItemViewModel>> catsHandler,
    IQueryHandlerAsync<GetCustomerCatVisitsQuery, IReadOnlyList<CustomerVisitListItemViewModel>?> visitsHandler)
    : IPortalSectionReader<PortalVisitsViewModel>
{
    public async Task<PortalVisitsViewModel> ReadAsync(PortalCaller caller)
    {
        var cats = (await catsHandler.HandleAsync(new GetCatsQuery
        {
            OwnerId = caller.CustomerId,
            IncludeDeceased = false,
            PageNumber = 1,
            PageSize = PortalRules.DashboardCatLimit,
            UserContext = caller.UserContext,
            CorrelationId = caller.CorrelationId,
        })).Result.Items;

        var latest = new List<PortalVisitItemViewModel>();

        foreach (var cat in cats)
        {
            var visits = (await visitsHandler.HandleAsync(new GetCustomerCatVisitsQuery
            {
                CatId = cat.Id, CustomerId = caller.CustomerId, UserContext = caller.UserContext, CorrelationId = caller.CorrelationId,
            })).Result;

            var newest = visits?.OrderByDescending(v => v.Date).FirstOrDefault();
            if (newest is null) continue;

            latest.Add(new PortalVisitItemViewModel(newest.Id, cat.Id, cat.Name, newest.Date, newest.Service, newest.Vet, newest.HasSummary, newest.FollowUpOn));
        }

        var items = latest.OrderByDescending(v => v.VisitedOn).ThenBy(v => v.CatName, StringComparer.Ordinal).Take(PortalRules.DashboardItemLimit).ToList();
        return new PortalVisitsViewModel(items);
    }
}

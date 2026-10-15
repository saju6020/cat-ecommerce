using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Portal.Sections;

/// <summary>
/// HO-CAT-001 Q2 + Q4 (HO-POR-001 §8.3). Asks for <c>DashboardCatLimit + 1</c> alive cats of the caller and derives <c>hasMore</c> from
/// the extra row (F31). Vaccination status is then read PER SHOWN CAT, one after another (F36), and ONLY for ids the owner-filtered
/// list just returned (ADR-261) -- so a cat id can never come from the client. A failing status read marks that cat
/// <c>vaccinationState = Unavailable</c>; the section stays <c>Ready</c>.
/// </summary>
public sealed class CatsSectionReader(
    IQueryHandlerAsync<GetCatsQuery, PagedResult<CatListItemViewModel>> catsHandler,
    IQueryHandlerAsync<GetCatVaccinationStatusQuery, CatVaccinationStatusViewModel?> vaccinationHandler,
    ILogger<CatsSectionReader> logger)
    : IPortalSectionReader<PortalCatsViewModel>
{
    public async Task<PortalCatsViewModel> ReadAsync(PortalCaller caller)
    {
        var response = await catsHandler.HandleAsync(new GetCatsQuery
        {
            OwnerId = caller.CustomerId,
            IncludeDeceased = false,
            PageNumber = 1,
            PageSize = PortalRules.DashboardCatLimit + 1,
            UserContext = caller.UserContext,
            CorrelationId = caller.CorrelationId,
        });

        var rows = response.Result.Items;
        var shown = rows.Take(PortalRules.DashboardCatLimit).ToList();
        var items = new List<PortalCatItemViewModel>(shown.Count);

        foreach (var cat in shown)
            items.Add(await MapAsync(cat, caller));

        return new PortalCatsViewModel(items.Count > 0, rows.Count > PortalRules.DashboardCatLimit, items);
    }

    private async Task<PortalCatItemViewModel> MapAsync(CatListItemViewModel cat, PortalCaller caller)
    {
        try
        {
            var status = (await vaccinationHandler.HandleAsync(new GetCatVaccinationStatusQuery
            {
                CatId = cat.Id, UserContext = caller.UserContext, CorrelationId = caller.CorrelationId,
            })).Result;

            if (status is not null)
                return new PortalCatItemViewModel(
                    cat.Id, cat.Name, cat.IsHighRisk, PortalSectionState.Ready.DisplayName,
                    new PortalVaccinationViewModel(status.OverallStatus, status.EarliestNextDueOn, status.BookingEligible));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Portal cat vaccination status unavailable. CorrelationId={CorrelationId}", caller.CorrelationId);
        }

        return new PortalCatItemViewModel(cat.Id, cat.Name, cat.IsHighRisk, PortalSectionState.Unavailable.DisplayName, null);
    }
}

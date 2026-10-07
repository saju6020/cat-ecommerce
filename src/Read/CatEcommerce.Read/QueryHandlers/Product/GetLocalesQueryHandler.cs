using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 12.4 -- <c>{ defaultLocale, locales[] }</c> from configuration.</summary>
public sealed class GetLocalesQueryHandler(IContentLocaleSettings localeSettings) : IQueryHandlerAsync<GetLocalesQuery, LocalesViewModel>
{
    public Task<QueryResponse<LocalesViewModel>> HandleAsync(GetLocalesQuery query) =>
        Task.FromResult(new QueryResponse<LocalesViewModel> { Result = new LocalesViewModel(localeSettings.DefaultLocale, localeSettings.EnabledLocales) });
}

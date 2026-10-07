using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Administration;

/// <summary>HO-ADM-001 §12.4 -- every registry key with bounds, unit, default and effective value
/// (US-ADM-05 AC1, I-D6).</summary>
public sealed class GetSettingsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetSettingsQuery, IReadOnlyList<SettingViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<SettingViewModel>>> HandleAsync(GetSettingsQuery query)
    {
        var globalValues = await LoadScopeAsync(SettingScopes.GlobalId);
        var branchValues = query.BranchId is { } branchId && branchId != SettingScopes.GlobalId ? await LoadScopeAsync(branchId) : null;

        var result = SettingDefinitions.All
            .OrderBy(d => d.Group).ThenBy(d => d.Key)
            .Select(d =>
            {
                var globalValue = globalValues.GetValueOrDefault(d.Key) ?? d.Default;
                var branchValue = branchValues?.GetValueOrDefault(d.Key);
                var effectiveValue = branchValue ?? globalValue;

                return new SettingViewModel(
                    d.Key, d.Group, d.Type.DisplayName, d.Min, d.Max, d.Unit, d.Default, d.BranchOverridable,
                    d.DisplayName, d.Description, globalValue, branchValue, effectiveValue);
            })
            .ToList();

        return new QueryResponse<IReadOnlyList<SettingViewModel>> { Result = result };
    }

    /// <summary>Gate fix 2026-09-26: previously loaded the full <c>BusinessSettingsAggregate</c>
    /// (every base-aggregate column) just to read its owned <c>Values</c> collection. Projects
    /// straight to that collection's own key/value pairs -- EF translates the nested owned
    /// collection to a correlated subquery -- so SQL never returns anything else.</summary>
    private async Task<Dictionary<string, string>> LoadScopeAsync(Guid scopeId)
    {
        var values = await db.Set<BusinessSettingsAggregate>().AsNoTracking()
            .Where(s => s.Id == scopeId)
            .Select(s => s.Values.Select(v => new { v.Key, v.Value }).ToList())
            .FirstOrDefaultAsync();

        return values?.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal) ?? [];
    }
}

using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;

namespace CatEcommerce.Read.QueryHandlers.Portal;

/// <summary>
/// HO-POR-001 §8.4, ADR-260 -- the ONLY catch site of the dashboard path (plus the per-cat vaccination read, which the handover
/// requires to degrade a single cat). One failing module marks its own section <c>Unavailable</c> (data <c>null</c>, never
/// <c>[]</c> or <c>0</c>) and the rest of the page is unaffected. Request cancellation is never swallowed. The log line carries the
/// section name, the exception and the correlation id -- no customer data and no upstream payload (I-X1).
/// </summary>
public sealed class PortalSectionComposer(ILogger<PortalSectionComposer> logger)
{
    /// <param name="sectionName">The fixed section key, for the log line only.</param>
    /// <param name="correlationId">The portal query's correlation id (added to the handover's two-argument signature so the line can carry it).</param>
    /// <param name="read">One reader call. Callers await sections one after another, never in parallel (ADR-267, F36).</param>
    public async Task<PortalSection<T>> RunAsync<T>(string sectionName, Guid correlationId, Func<Task<T>> read) where T : class
    {
        try
        {
            return new PortalSection<T>(PortalSectionState.Ready.DisplayName, await read());
        }
        catch (OperationCanceledException)
        {
            throw; // the request was aborted -- never swallow
        }
        catch (Exception ex)
        {
            logger.LogError(ex, PortalMessages.SectionUnavailableLogTemplate, sectionName, correlationId);
            return new PortalSection<T>(PortalSectionState.Unavailable.DisplayName, null);
        }
    }
}

using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Billing;

/// <summary>
/// HO-BIL-001 §8.5 (ADR-178) -- the body every Phase J capture handler shares (the appointment handler of Phase B keeps its own copy, unchanged): dispatch
/// <see cref="CaptureSourceChargesCommand"/> in a <b>new DI scope</b> (Billing's <c>DbContext</c> is separate from the source's, so a Billing failure can never
/// poison or fail the source's request), catch everything, log <b>ids and the exception type only</b>, and return. A capture that failed is visible in
/// <c>GetUncapturedSourcesQuery</c> and recoverable by hand, never lost. Never enqueues a message (HO-NTF-001 ADR-057).
/// </summary>
internal static class SourceChargeCapture
{
    public static async Task RunAsync(IServiceScopeFactory scopeFactory, ILogger logger, ChargeSourceKind kind, Guid sourceId, Event origin)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();

            var response = await dispatcher.SendAsync(new CaptureSourceChargesCommand
            {
                SourceKind = kind.DisplayName,
                SourceId = sourceId,
                UserContext = origin.UserContext,
                CorrelationId = origin.CorrelationId,
                ValidateCommand = true,
            });

            if (!response.IsSuccess)
                logger.LogError("Charge capture for {SourceKind} {SourceId} was refused ({Codes}).", kind.DisplayName, sourceId,
                    string.Join(",", response.ValidationResult?.Errors.Select(e => e.ErrorCode) ?? []));
            else if (response.Result is CaptureSourceChargesResult { Skipped.Count: > 0 } captured)
                // A line the capture could not record (no price, no tax class, a refused description) is lost revenue unless someone sees it: the source stays on the
                // uncaptured list (it has fewer charges than billable lines) and this says why. Ids and reason codes only.
                logger.LogWarning("Charge capture for {SourceKind} {SourceId} recorded {Captured} and skipped {Skipped} ({Reasons}).", kind.DisplayName, sourceId,
                    captured.Captured, captured.Skipped.Count, string.Join(",", captured.Skipped.Select(s => s.Reason).Distinct()));
        }
        catch (Exception ex)
        {
            logger.LogError("Charge capture for {SourceKind} {SourceId} failed ({ExceptionType}).", kind.DisplayName, sourceId, ex.GetType().Name);
        }
    }
}

using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.Events.Clinic;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Billing;

/// <summary>
/// HO-BIL-001 §8.5 / §11.6 (ADR-178) -- thin. When an appointment completes, dispatch
/// <see cref="CaptureSourceChargesCommand"/> in a <b>new DI scope</b> (so Billing's <c>DbContext</c> is separate
/// from the appointment's: a Billing failure can never poison the source's context or fail the source's request --
/// <c>.ai/domain-events-and-outbox.md</c> §3), catch every exception, log an Error with <b>ids only</b>, and return.
/// A capture that fails is therefore visible in <c>GetUncapturedSourcesQuery</c> and recoverable by hand, never lost.
/// Never enqueues a message (HO-NTF-001 ADR-057).
/// </summary>
public sealed class CaptureChargesOnAppointmentCompletedHandler(
    IServiceScopeFactory scopeFactory, ILogger<CaptureChargesOnAppointmentCompletedHandler> logger)
    : IEventHandlerAsync<AppointmentCompletedEvent>
{
    public async Task HandleAsync(AppointmentCompletedEvent @event)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();

            var response = await dispatcher.SendAsync(new CaptureSourceChargesCommand
            {
                SourceKind = ChargeSourceKind.Appointment.DisplayName,
                SourceId = @event.AppointmentId,
                UserContext = @event.UserContext,
                CorrelationId = @event.CorrelationId,
                ValidateCommand = true,
            });

            if (!response.IsSuccess)
                logger.LogError("Charge capture for appointment {AppointmentId} was refused ({Codes}).", @event.AppointmentId,
                    string.Join(",", response.ValidationResult?.Errors.Select(e => e.ErrorCode) ?? []));
        }
        catch (Exception ex)
        {
            // Ids and the exception type only -- never the message, which could echo input.
            logger.LogError("Charge capture for appointment {AppointmentId} failed ({ExceptionType}).", @event.AppointmentId, ex.GetType().Name);
        }
    }
}

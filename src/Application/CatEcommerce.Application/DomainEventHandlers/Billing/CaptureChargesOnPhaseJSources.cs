using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Prescription;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Billing;

// HO-BIL-001 Phase J (owner decisions 2026-10-14) -- the thin handlers that start a capture. Each is the Phase B shape: a fresh scope, the one command, every
// exception caught and logged with ids only (SourceChargeCapture). They name the REAL events the merged areas raise, which differ from section 8.13's assumed
// ones; the differences are recorded in HO-BIL-001 section 1 Amended.

/// <summary>EMR: <see cref="VisitFinalisedEvent"/> as built (it now carries <c>CustomerId</c>; it has no <c>BoardingStayId</c>, deferred to HO-BRD-001).</summary>
public sealed class CaptureChargesOnVisitFinalisedHandler(IServiceScopeFactory scopeFactory, ILogger<CaptureChargesOnVisitFinalisedHandler> logger)
    : IEventHandlerAsync<VisitFinalisedEvent>
{
    public Task HandleAsync(VisitFinalisedEvent @event) =>
        SourceChargeCapture.RunAsync(scopeFactory, logger, ChargeSourceKind.Visit, @event.VisitId, @event);
}

/// <summary>PHM: <see cref="PrescriptionDispensedEvent"/> as built (<c>DispensingId</c> is the source id; it carries the price snapshot per line, which the source re-reads from the dispensing).</summary>
public sealed class CaptureChargesOnPrescriptionDispensedHandler(IServiceScopeFactory scopeFactory, ILogger<CaptureChargesOnPrescriptionDispensedHandler> logger)
    : IEventHandlerAsync<PrescriptionDispensedEvent>
{
    public Task HandleAsync(PrescriptionDispensedEvent @event) =>
        SourceChargeCapture.RunAsync(scopeFactory, logger, ChargeSourceKind.Dispense, @event.DispensingId, @event);
}

/// <summary>SRG: there is no <c>SurgeryCompletedEvent</c>. A surgery ends with <see cref="SurgeryDischargedEvent"/>, which is what Billing captures from.</summary>
public sealed class CaptureChargesOnSurgeryDischargedHandler(IServiceScopeFactory scopeFactory, ILogger<CaptureChargesOnSurgeryDischargedHandler> logger)
    : IEventHandlerAsync<SurgeryDischargedEvent>
{
    public Task HandleAsync(SurgeryDischargedEvent @event) =>
        SourceChargeCapture.RunAsync(scopeFactory, logger, ChargeSourceKind.Surgery, @event.SurgeryId, @event);
}

/// <summary>
/// PHM: a supply reversed inside its window (<see cref="DispensingReversedEvent"/>) must not stay billed. Billing voids that dispensing's charges that are
/// <b>not yet on an invoice</b>, in a fresh scope, one <c>VoidChargeCommand</c> each (idempotent: a voided charge answers a violation, which is logged and
/// ignored). A charge already invoiced cannot be voided -- the staff correct it with a credit note (US-BIL-05) -- so it is logged as a warning with ids only.
/// </summary>
public sealed class VoidChargesOnDispensingReversedHandler(IServiceScopeFactory scopeFactory, ILogger<VoidChargesOnDispensingReversedHandler> logger)
    : IEventHandlerAsync<DispensingReversedEvent>
{
    public const string VoidReason = "The dispensing was reversed.";

    public async Task HandleAsync(DispensingReversedEvent @event)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var finder = scope.ServiceProvider.GetRequiredService<IBillingFinder>();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();

            foreach (var chargeId in await finder.FindChargeIdsBySourceAsync(ChargeSourceKind.Dispense, @event.DispensingId))
            {
                var response = await dispatcher.SendAsync(new VoidChargeCommand
                {
                    ChargeId = chargeId, BranchId = @event.BranchId, Reason = VoidReason,
                    UserContext = @event.UserContext, CorrelationId = @event.CorrelationId, ValidateCommand = true,
                });

                if (!response.IsSuccess)
                    logger.LogWarning("Charge {ChargeId} of reversed dispensing {DispensingId} was not voided ({Codes}); an invoiced charge needs a credit note.",
                        chargeId, @event.DispensingId, string.Join(",", response.ValidationResult?.Errors.Select(e => e.ErrorCode) ?? []));
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Voiding the charges of reversed dispensing {DispensingId} failed ({ExceptionType}).", @event.DispensingId, ex.GetType().Name);
        }
    }
}

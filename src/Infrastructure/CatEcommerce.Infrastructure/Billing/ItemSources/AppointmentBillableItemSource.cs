using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Billing.ItemSources;

/// <summary>
/// HO-BIL-001 §8.5 (Phase B) -- for a <c>Completed</c> appointment: one charge, <c>ItemKind = Service</c>,
/// <c>Clinic</c>, <c>Description = ServiceSnapshot.Name</c>, quantity 1, the snapshot price (never re-read --
/// ADR-049), the service's <b>current</b> tax class (F34: the snapshot carries none), <c>ServiceDate</c> = the
/// local date of <c>CompletedOn</c> in the branch zone, line ref <c>"service"</c>. Walk-ins are appointments and
/// are charged the same way. The appointment fee is charged here and only here (OQ-14).
/// </summary>
public sealed class AppointmentBillableItemSource(CatEcommerceDbContext db, IBillingPricingInputs pricingInputs) : IBillableItemSource
{
    public const string ServiceLineRef = "service";

    public ChargeSourceKind Kind => ChargeSourceKind.Appointment;

    public async Task<BillableSourceResult> GetItemsAsync(Guid sourceId, CancellationToken ct = default)
    {
        var appointment = await db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => a.Id == sourceId && !a.IsMarkedToDelete)
            .Select(a => new
            {
                a.Id, a.BranchId, a.CustomerId, a.CatId, a.ClinicServiceId, a.Status, a.CompletedOn, a.SurgeryId,
                Name = a.ServiceSnapshot.Name, PriceAmount = a.ServiceSnapshot.Price.Amount, PriceCurrency = a.ServiceSnapshot.Price.CurrencyCode,
            })
            .FirstOrDefaultAsync(ct);

        if (appointment is null) return BillableSourceResult.NotFound;
        // A surgery's appointment is completed by the surgery (CompleteSurgerySlot) but its fee belongs to the SURGERY source (the price snapshot, campaign price
        // included). One fee has one owning source (section 8.5, OQ-14): refuse it here, whoever asks and however the capture is started.
        if (appointment.SurgeryId is not null)
            return BillableSourceResult.NotBillable("AppointmentManagedBySurgery").InBranch(appointment.BranchId);
        if (appointment.Status != AppointmentStatus.Completed || appointment.CompletedOn is null)
            return BillableSourceResult.NotBillable("AppointmentNotCompleted").InBranch(appointment.BranchId);

        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking()
            .Where(b => b.Id == appointment.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync(ct) ?? "UTC";
        var serviceDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(appointment.CompletedOn.Value, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).DateTime);

        var taxClassId = await pricingInputs.GetServiceTaxClassIdAsync(appointment.ClinicServiceId, ct);

        var item = new BillableSourceItem(
            appointment.CustomerId, appointment.BranchId, appointment.CatId, ItemKind.Service, appointment.Name, 1m,
            new Money(appointment.PriceAmount, appointment.PriceCurrency), new Money(0m, appointment.PriceCurrency), taxClassId, serviceDate,
            ServiceLineRef, null, null);

        return BillableSourceResult.Billable([item]).InBranch(appointment.BranchId);
    }
}

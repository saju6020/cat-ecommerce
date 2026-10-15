using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BIL-001 §16 group B (I-B1…I-B5, U-B3) -- charges reach Billing from appointments without loss;
/// failures are listed and recoverable; the push port is idempotent by source key (ADR-178).</summary>
public sealed class BillingChargeEndpointTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    private async Task<JsonElement> ChargesOfAsync(BillingContext context, Guid customerId, string? status = null)
    {
        var url = $"/api/staff/billing/charges?customerId={customerId}" + (status is null ? "" : $"&status={status}");
        var page = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync(url)));
        return page.GetProperty("items");
    }

    private async Task SetServiceTaxClassAsync(Guid serviceId, Guid taxClassId) =>
        await WithDbAsync(async db => await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE admin.clinic_service SET tax_class_id = {taxClassId} WHERE \"Id\" = {serviceId}"));

    /// <summary>I-B1 -- completing an appointment records one charge whose price is the appointment's snapshot, even
    /// after the service price changed (HO-ADM-001 ADR-049).</summary>
    [Fact]
    public async Task I_B1_CompletingAnAppointment_RecordsOneCharge_AtTheSnapshotPrice_EvenAfterThePriceChanged()
    {
        var context = await CreateBillingContextAsync();
        var clinic = await SeedClinicAsync(context, price: 800m);
        var customerId = await SeedCustomerAsync(context);

        var appointmentId = await RegisterWalkInAsync(context, clinic, customerId);

        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await SetServicePriceAsync(context, clinic.ServiceId, 999m);

        await ExpectAsync(HttpStatusCode.NoContent, await CompleteAppointmentAsync(context, clinic, appointmentId));

        var charges = await ChargesOfAsync(context, customerId);
        var charge = Assert.Single(charges.EnumerateArray());
        Assert.Equal("800.00", charge.GetProperty("unitPrice").GetProperty("amount").GetString());
        Assert.Equal("Appointment", charge.GetProperty("sourceKind").GetString());
        Assert.Equal(appointmentId, charge.GetProperty("sourceId").GetGuid());
        Assert.Equal("service", charge.GetProperty("sourceLineRef").GetString());
        Assert.Equal("Clinic", charge.GetProperty("businessLine").GetString());
        Assert.Equal("Service", charge.GetProperty("itemKind").GetString());
        Assert.Equal(context.TaxClassId, charge.GetProperty("taxClassId").GetGuid());
        Assert.Equal(Today.ToString("yyyy-MM-dd"), charge.GetProperty("serviceDate").GetString());
    }

    /// <summary>I-B2 -- a capture that breaks never fails the appointment's request; the appointment is listed
    /// as uncaptured, and after fixing the data a manual capture records the charge.</summary>
    [Fact]
    public async Task I_B2_ABrokenCapture_DoesNotFailTheAppointment_IsListed_AndIsRecoverable()
    {
        var context = await CreateBillingContextAsync();
        var clinic = await SeedClinicAsync(context);
        var customerId = await SeedCustomerAsync(context);
        var appointmentId = await RegisterWalkInAsync(context, clinic, customerId);

        await SetServiceTaxClassAsync(clinic.ServiceId, Guid.NewGuid()); // the tax class "disappears"
        await ExpectAsync(HttpStatusCode.NoContent, await CompleteAppointmentAsync(context, clinic, appointmentId));

        Assert.Empty((await ChargesOfAsync(context, customerId)).EnumerateArray());

        var uncaptured = await ReadAsync(await ExpectAsync(HttpStatusCode.OK,
            await context.Admin.GetAsync($"/api/staff/billing/uncaptured-sources?from={Today.AddDays(-1):yyyy-MM-dd}&to={Today.AddDays(1):yyyy-MM-dd}")));
        var entry = uncaptured.EnumerateArray().Single(e => e.GetProperty("sourceId").GetGuid() == appointmentId);
        Assert.Equal("Appointment", entry.GetProperty("sourceKind").GetString());
        Assert.Equal(customerId, entry.GetProperty("customerId").GetGuid());

        var stillBroken = await ReadAsync(await ExpectAsync(HttpStatusCode.OK,
            await context.Admin.PostAsync($"/api/staff/billing/sources/Appointment/{appointmentId}/capture", null)));
        Assert.Equal(0, stillBroken.GetProperty("captured").GetInt32());
        Assert.Equal("TaxClassNotFound", Assert.Single(stillBroken.GetProperty("skipped").EnumerateArray()).GetProperty("reason").GetString());

        await SetServiceTaxClassAsync(clinic.ServiceId, context.TaxClassId); // fixed

        var recovered = await ReadAsync(await ExpectAsync(HttpStatusCode.OK,
            await context.Admin.PostAsync($"/api/staff/billing/sources/Appointment/{appointmentId}/capture", null)));
        Assert.Equal(1, recovered.GetProperty("captured").GetInt32());
        Assert.Single((await ChargesOfAsync(context, customerId)).EnumerateArray());

        var after = await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/uncaptured-sources?from={Today.AddDays(-1):yyyy-MM-dd}&to={Today.AddDays(1):yyyy-MM-dd}"));
        Assert.DoesNotContain(after.EnumerateArray(), e => e.GetProperty("sourceId").GetGuid() == appointmentId);

        var again = await ReadAsync(await context.Admin.PostAsync($"/api/staff/billing/sources/Appointment/{appointmentId}/capture", null));
        Assert.Equal(0, again.GetProperty("captured").GetInt32()); // U-B6 end to end
    }

    [Fact]
    public async Task ACaptureOfAnAppointmentThatIsNotCompleted_IsAZeroWithTheReason_AndAnUnknownOneIs404()
    {
        var context = await CreateBillingContextAsync();
        var clinic = await SeedClinicAsync(context);
        var customerId = await SeedCustomerAsync(context);
        var appointmentId = await RegisterWalkInAsync(context, clinic, customerId);

        var result = await ReadAsync(await ExpectAsync(HttpStatusCode.OK,
            await context.Admin.PostAsync($"/api/staff/billing/sources/Appointment/{appointmentId}/capture", null)));
        Assert.Equal(0, result.GetProperty("captured").GetInt32());
        Assert.Equal("AppointmentNotCompleted", Assert.Single(result.GetProperty("skipped").EnumerateArray()).GetProperty("reason").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await context.Admin.PostAsync($"/api/staff/billing/sources/Appointment/{Guid.NewGuid()}/capture", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await context.Admin.PostAsync($"/api/staff/billing/sources/Order/{Guid.NewGuid()}/capture", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await context.Admin.PostAsync($"/api/staff/billing/sources/Nonsense/{Guid.NewGuid()}/capture", null)).StatusCode);
    }

    /// <summary>I-B3 -- a manual charge for a service uses the price at now; a free-text fee is as entered.</summary>
    [Fact]
    public async Task I_B3_ManualCharges_ForAService_AndForAFreeTextFee()
    {
        var context = await CreateBillingContextAsync();
        var clinic = await SeedClinicAsync(context, price: 650m);
        var customerId = await SeedCustomerAsync(context);

        var service = await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/staff/billing/charges",
            new { customerId, serviceId = clinic.ServiceId, quantity = 2, serviceDate = Today.ToString("yyyy-MM-dd") }, Json));
        var serviceChargeId = (await ReadAsync(service)).GetProperty("chargeId").GetGuid();

        var fee = await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/staff/billing/charges", new
        {
            customerId, description = "Late pickup fee", unitPrice = 125.50m, taxClassId = context.ZeroTaxClassId, quantity = 1, lineDiscount = 5.50m,
            serviceDate = Today.ToString("yyyy-MM-dd"), businessLine = "Boarding",
        }, Json));
        var feeChargeId = (await ReadAsync(fee)).GetProperty("chargeId").GetGuid();

        var charges = (await ChargesOfAsync(context, customerId)).EnumerateArray().ToList();
        var byService = charges.Single(c => c.GetProperty("chargeId").GetGuid() == serviceChargeId);
        Assert.Equal("650.00", byService.GetProperty("unitPrice").GetProperty("amount").GetString());
        Assert.Equal("2.000", byService.GetProperty("quantity").GetString());
        Assert.Equal("1300.00", byService.GetProperty("lineTotal").GetProperty("amount").GetString());
        Assert.Equal("Manual", byService.GetProperty("sourceKind").GetString());
        Assert.Equal("Service", byService.GetProperty("itemKind").GetString());

        var byFee = charges.Single(c => c.GetProperty("chargeId").GetGuid() == feeChargeId);
        Assert.Equal("Late pickup fee", byFee.GetProperty("description").GetString());
        Assert.Equal("125.50", byFee.GetProperty("unitPrice").GetProperty("amount").GetString());
        Assert.Equal("120.00", byFee.GetProperty("lineTotal").GetProperty("amount").GetString());
        Assert.Equal("Fee", byFee.GetProperty("itemKind").GetString());
        Assert.Equal("Boarding", byFee.GetProperty("businessLine").GetString());
    }

    [Fact]
    public async Task AManualCharge_ForAnUnknownCustomer_OrWithBothForms_OrABadShape_Is400()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var date = Today.ToString("yyyy-MM-dd");

        Assert.Equal(HttpStatusCode.BadRequest, (await context.Admin.PostAsJsonAsync("/api/staff/billing/charges",
            new { customerId = Guid.NewGuid(), description = "x", unitPrice = 1, taxClassId = context.TaxClassId, quantity = 1, serviceDate = date }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await context.Admin.PostAsJsonAsync("/api/staff/billing/charges",
            new { customerId, serviceId = Guid.NewGuid(), description = "x", unitPrice = 1, taxClassId = context.TaxClassId, quantity = 1, serviceDate = date }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await context.Admin.PostAsJsonAsync("/api/staff/billing/charges",
            new { customerId, description = "x", unitPrice = 1.005m, taxClassId = context.TaxClassId, quantity = 1, serviceDate = date }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await context.Admin.PostAsJsonAsync("/api/staff/billing/charges",
            new { customerId, description = "x", unitPrice = 1, taxClassId = Guid.NewGuid(), quantity = 1, serviceDate = date }, Json)).StatusCode);
    }

    /// <summary>I-B4 (first half) -- an unbilled charge is voided with a reason; voiding twice is refused.</summary>
    [Fact]
    public async Task I_B4_AnUnbilledCharge_IsVoided_WithAReason_ThenItLeavesTheUnbilledList()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var chargeId = await RecordChargeAsync(context, customerId);

        Assert.Equal(HttpStatusCode.BadRequest, (await context.Admin.PostAsJsonAsync($"/api/staff/billing/charges/{chargeId}/void", new { reason = "" }, Json)).StatusCode);
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsJsonAsync($"/api/staff/billing/charges/{chargeId}/void", new { reason = "Entered twice" }, Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await context.Admin.PostAsJsonAsync($"/api/staff/billing/charges/{chargeId}/void", new { reason = "Again" }, Json)).StatusCode);

        Assert.Empty((await ChargesOfAsync(context, customerId, "unbilled")).EnumerateArray());
        var voided = Assert.Single((await ChargesOfAsync(context, customerId, "voided")).EnumerateArray());
        Assert.Equal("Entered twice", voided.GetProperty("voidReason").GetString());
    }

    /// <summary>U-B3 -- the recorder returns the existing id for a source key already tracked in the context, and
    /// for one already in the table; nothing extra is added.</summary>
    [Fact]
    public async Task U_B3_TheRecorder_IsIdempotentBySourceKey_TrackedAndPersisted()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var sourceId = Guid.NewGuid();
        var request = new ChargeRequestDto(
            customerId, context.BranchId, null, BusinessLine.Shop, ItemKind.Product, ChargeSourceKind.Order, sourceId, "line-1", null, null,
            "Cat food 2kg", 2m, new Money(450m, "BDT"), new Money(0m, "BDT"), context.TaxClassId, Today);

        Guid first, second;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var recorder = scope.ServiceProvider.GetRequiredService<IBillableChargeRecorder>();
            first = await recorder.RecordAsync(request);
            second = await recorder.RecordAsync(request with { Description = "different text, same key" });
            Assert.Equal(first, second);
            await scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>().SaveChangesAsync();
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var recorder = scope.ServiceProvider.GetRequiredService<IBillableChargeRecorder>();
            var third = await recorder.RecordAsync(request);
            Assert.Equal(first, third);
            await scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>().SaveChangesAsync();
        }

        Assert.Equal(1, await CountAsync<ChargeAggregate>(c => c.SourceId == sourceId));
    }

    /// <summary>The port validates, never reprices: a bad quantity or a foreign currency is a domain violation.</summary>
    [Fact]
    public async Task TheRecorder_ValidatesTheRequest_ItNeverReprices()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var bad = new ChargeRequestDto(
            customerId, context.BranchId, null, BusinessLine.Shop, ItemKind.Product, ChargeSourceKind.Order, Guid.NewGuid(), "main", null, null,
            "x", 1m, new Money(10m, "USD"), new Money(0m, "USD"), context.TaxClassId, Today);

        await using var scope = Factory.Services.CreateAsyncScope();
        var recorder = scope.ServiceProvider.GetRequiredService<IBillableChargeRecorder>();

        var ex = await Assert.ThrowsAsync<CatEcommerce.Domain.Exceptions.DomainRuleViolationException>(() => recorder.RecordAsync(bad));
        Assert.Equal(BusinessViolationCodes.CurrencyMismatch, ex.ErrorCode);
        await Assert.ThrowsAsync<CatEcommerce.Domain.Exceptions.DomainRuleViolationException>(() => recorder.RecordAsync(bad with { UnitPrice = new Money(10m, "BDT"), LineDiscount = new Money(0m, "BDT"), Quantity = 0m }));
    }

    /// <summary>The recorder's void: absent is a no-op; an unbilled charge is voided in the caller's unit of work.</summary>
    [Fact]
    public async Task TheRecorderVoid_AbsentIsANoOp_AndAnUnbilledChargeIsVoidedInTheCallersUnitOfWork()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var sourceId = Guid.NewGuid();
        await RecordChargeAsync(context, customerId, sourceId: sourceId, sourceLineRef: "night-1");

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var recorder = scope.ServiceProvider.GetRequiredService<IBillableChargeRecorder>();
            await recorder.VoidAsync(new ChargeSourceKey(ChargeSourceKind.Order, Guid.NewGuid(), "night-1"), "nothing there");
            await recorder.VoidAsync(new ChargeSourceKey(ChargeSourceKind.Order, sourceId, "night-1"), "Night cancelled");
            await scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>().SaveChangesAsync();
        }

        var charge = await WithDbAsync(db => db.Charges.AsNoTracking().SingleAsync(c => c.SourceId == sourceId));
        Assert.Equal(ChargeStatus.Voided, charge.Status);
        Assert.Equal("Night cancelled", charge.VoidReason);
    }

    /// <summary>I-B5 -- two requests record the same source key concurrently: never two rows. The loser is either
    /// deduplicated by the pre-check or surfaces the unique index (<c>409 ChargeAlreadyRecorded</c> at the edge).</summary>
    [Fact]
    public async Task I_B5_TheSameSourceKeyRecordedConcurrently_NeverMakesTwoRows()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var sourceId = Guid.NewGuid();

        async Task<(bool Saved, bool UniqueViolation)> TryRecord()
        {
            try
            {
                await RecordChargeAsync(context, customerId, sourceId: sourceId, sourceLineRef: "night-1");
                return (true, false);
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { ConstraintName: "ix_charge_source" })
            {
                return (false, true);
            }
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(TryRecord)));

        Assert.Contains(results, r => r.Saved);
        Assert.Equal(1, await CountAsync<ChargeAggregate>(c => c.SourceId == sourceId));
    }

    [Fact]
    public async Task TheRaceOnTheSourceKey_SurfacesAs409ChargeAlreadyRecorded_NotAn500()
    {
        // The filter mapping is exercised directly in UniqueConstraintExceptionFilterTests; here the unique index
        // itself is proven to be the guard when the pre-check cannot see a concurrent insert.
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var sourceId = Guid.NewGuid();
        var chargeId = await RecordChargeAsync(context, customerId, sourceId: sourceId);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await WithDbAsync(async db =>
            {
                var duplicate = new ChargeAggregate(Guid.NewGuid());
                duplicate.Record(new ChargeRequestDto(
                    customerId, context.BranchId, null, BusinessLine.Shop, ItemKind.Product, ChargeSourceKind.Order, sourceId, "main", null, null,
                    "dup", 1m, new Money(1m, "BDT"), new Money(0m, "BDT"), context.TaxClassId, Today),
                    new ChargeRecordContext("BDT", Now, new BillingActor(new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid())));
                db.Charges.Add(duplicate);
                await db.SaveChangesAsync();
                return 0;
            }));

        Assert.Equal("ix_charge_source", Assert.IsType<Npgsql.PostgresException>(ex.InnerException).ConstraintName);
        Assert.NotEqual(Guid.Empty, chargeId);
    }

    [Fact]
    public async Task ChargesOfAnotherBranch_AreNotVisible_AndNotVoidable()
    {
        var mine = await CreateBillingContextAsync();
        var other = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(mine);
        var chargeId = await RecordChargeAsync(mine, customerId);

        Assert.Empty((await ChargesOfAsync(other, customerId)).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.PostAsJsonAsync($"/api/staff/billing/charges/{chargeId}/void", new { reason = "not mine" }, Json)).StatusCode);
        Assert.Single((await ChargesOfAsync(mine, customerId)).EnumerateArray());
    }

    [Fact]
    public async Task ChargeEndpoints_AreRestrictedToBillingStaff()
    {
        var context = await CreateBillingContextAsync();
        var (vet, _) = await StaffClientAsync(context, StaffRoles.Vet);
        var (reception, _) = await StaffClientAsync(context, StaffRoles.Reception);

        Assert.Equal(HttpStatusCode.Forbidden, (await vet.GetAsync("/api/staff/billing/charges")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vet.PostAsJsonAsync("/api/staff/billing/charges", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync("/api/staff/billing/charges")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClient().GetAsync("/api/staff/billing/charges")).StatusCode);
    }
}

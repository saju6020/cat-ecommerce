using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Read.QueryHandlers.Surgery;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Queries;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SRG-001 phase F -- US-SRG-10 (the recovery board and status), US-SRG-07 (discharge, post-op instructions, follow-ups and their reminders) and the customer's own view. The delivery
/// pass is the real HO-NTF-001 one under the fake clock, so the class is in the <c>Notifications</c> collection.
/// </summary>
[Collection("Notifications")]
public sealed class SurgeryRecoveryEndpointTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    private static Task<HttpResponseMessage> EndAsync(HttpClient client, Guid surgeryId) =>
        client.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/end-procedure", new { outcome = "Completed" }, Json);

    private static Task<HttpResponseMessage> SetStatusAsync(HttpClient client, Guid surgeryId, string status, string? note = null) =>
        client.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/recovery-status", new { status, note }, Json);

    private static Task<HttpResponseMessage> DischargeAsync(HttpClient client, Guid surgeryId, object? body = null) =>
        client.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/discharge", body ?? new { }, Json);

    /// <summary>A surgery ended and ready for discharge.</summary>
    private async Task<Guid> ReadyForDischargeAsync(SurgeryWorld world, Guid? catId = null, int hour = 10)
    {
        var (surgeryId, _) = await InTheatreAsync(world, catId: catId, hour: hour);
        Assert.Equal(HttpStatusCode.NoContent, (await EndAsync(world.Vet, surgeryId)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SetStatusAsync(world.Nurse, surgeryId, "ReadyForDischarge")).StatusCode);
        return surgeryId;
    }

    private static async Task<NotificationRequestAggregate[]> RequestsAsync(SurgeryWorld world, string templateKey, Guid surgeryId)
    {
        await using var scope = world.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        return await db.Set<NotificationRequestAggregate>().AsNoTracking().Where(r => r.TemplateKey == templateKey && r.RecipientId == world.CustomerId && (r.SubjectId == null || r.SubjectId == surgeryId)).ToArrayAsync();
    }

    private static async Task<JsonElement> BoardAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/staff/surgeries/recovery-board");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    // ── US-SRG-10 ───────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-F1 -- set the recovery statuses; the board shows the current status and who changed it; the same status is a no-op; the history is on the detail.</summary>
    [Fact]
    public async Task I_F1_TheBoardShowsTheCurrentStatus_AndWhoChangedIt()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        await EndAsync(world.Vet, surgeryId);

        var first = Assert.Single((await BoardAsync(world.Reception)).GetProperty("entries").EnumerateArray());
        Assert.Equal(surgeryId, first.GetProperty("surgeryId").GetGuid());
        Assert.Equal("Recovering", first.GetProperty("recoveryStatus").GetString());
        Assert.Equal(world.VetId, first.GetProperty("statusChangedBy").GetGuid());
        Assert.Equal(world.CatId, first.GetProperty("catId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("customerName").GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await SetStatusAsync(world.Nurse, surgeryId, "Stable", "Eating a little")).StatusCode);
        var stable = Assert.Single((await BoardAsync(world.Reception)).GetProperty("entries").EnumerateArray());
        Assert.Equal("Stable", stable.GetProperty("recoveryStatus").GetString());
        Assert.Equal(world.NurseId, stable.GetProperty("statusChangedBy").GetGuid());

        // The same status again changes nothing.
        Assert.Equal(HttpStatusCode.NoContent, (await SetStatusAsync(world.Nurse, surgeryId, "Stable")).StatusCode);
        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal(2, detail.GetProperty("recoveryHistory").GetArrayLength()); // Recovering at the end of the procedure, then Stable
        Assert.Equal("Stable", detail.GetProperty("recoveryStatus").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await SetStatusAsync(world.Vet, surgeryId, "Recovering")).StatusCode); // any to any
        Assert.Equal("Recovering", Assert.Single((await BoardAsync(world.Nurse)).GetProperty("entries").EnumerateArray()).GetProperty("recoveryStatus").GetString());
    }

    [Fact]
    public async Task TheRecoveryStatus_IsAClinicalActsOnly_AValidStatus_AndOnlyWhileInRecovery()
    {
        var world = await SeedSurgeryWorldAsync();
        var scheduled = await BookAsync(world, LocalStart(world, 2, 10));
        var early = await SetStatusAsync(world.Nurse, scheduled, "Stable");
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Equal([BusinessViolationCodes.InvalidSurgeryTransition], await CodesOfAsync(early));

        var other = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(other);
        await EndAsync(other.Vet, surgeryId);

        Assert.Equal(HttpStatusCode.Forbidden, (await SetStatusAsync(other.Reception, surgeryId, "Stable")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetStatusAsync(other.Admin, surgeryId, "Stable")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatusAsync(other.Nurse, surgeryId, "Sleeping")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetStatusAsync(world.Nurse, surgeryId, "Stable")).StatusCode); // another branch's surgery
        Assert.Equal(HttpStatusCode.OK, (await other.Reception.GetAsync("/api/staff/surgeries/recovery-board")).StatusCode);
        Assert.Empty((await BoardAsync(world.Reception)).GetProperty("entries").EnumerateArray()); // the board is the active branch's only
    }

    /// <summary>I-F2 -- the board is one projection plus one name lookup: at most two SQL commands whatever the number of entries.</summary>
    [Fact]
    public async Task I_F2_TheRecoveryBoard_IsAtMostTwoSqlCommands_WhateverTheNumberOfEntries()
    {
        var world = await SeedSurgeryWorldAsync();
        foreach (var hour in new[] { 10, 12, 14 })
        {
            var cat = await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: $"Board{hour}");
            var (surgeryId, _) = await InTheatreAsync(world, catId: cat, hour: hour);
            await EndAsync(world.Vet, surgeryId);
        }

        await using var scope = world.Api.Services.CreateAsyncScope();
        var recorder = new CommandRecorder();
        var baseOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<CatEcommerceDbContext>>();
        await using var countingDb = new CatEcommerceDbContext(new DbContextOptionsBuilder<CatEcommerceDbContext>(baseOptions).AddInterceptors(recorder).Options);

        var result = await new GetRecoveryBoardQueryHandler(countingDb, TimeProvider.System).HandleAsync(new GetRecoveryBoardQuery { BranchId = world.BranchId, CorrelationId = Guid.NewGuid() });

        Assert.Equal(3, result.Result!.Entries.Count);
        Assert.True(recorder.Commands.Count <= 2, $"{recorder.Commands.Count} SQL commands:\n{string.Join("\n---\n", recorder.Commands)}");
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    // ── US-SRG-07 ───────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A surgery that is not ReadyForDischarge is refused; the draft shows the protocol defaults with their windows.</summary>
    [Fact]
    public async Task Discharge_NeedsReadyForDischarge_AndTheDraftShowsTheDefaults()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        await EndAsync(world.Vet, surgeryId);
        await SetStatusAsync(world.Nurse, surgeryId, "Stable");

        var refused = await DischargeAsync(world.Nurse, surgeryId);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal([BusinessViolationCodes.NotReadyForDischarge], await CodesOfAsync(refused));

        var draft = await world.Nurse.GetFromJsonAsync<JsonElement>($"/api/staff/surgeries/{surgeryId}/discharge-draft", Json);
        Assert.Contains("indoors", draft.GetProperty("postOpInstructions").GetString());
        var today = LocalToday(world);
        var followUps = draft.GetProperty("followUps").EnumerateArray().ToArray();
        Assert.Equal(["WoundCheck", "SutureRemoval"], followUps.Select(f => f.GetProperty("kind").GetString()).ToArray());
        Assert.Equal(today.AddDays(3).ToString("yyyy-MM-dd"), followUps[0].GetProperty("dueOn").GetString());
        Assert.Equal(today.AddDays(2).ToString("yyyy-MM-dd"), followUps[0].GetProperty("windowFrom").GetString());
        Assert.Equal(today.AddDays(14).ToString("yyyy-MM-dd"), followUps[1].GetProperty("windowTo").GetString());

        var other = await SeedSurgeryWorldAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Nurse.GetAsync($"/api/staff/surgeries/{surgeryId}/discharge-draft")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.GetAsync($"/api/staff/surgeries/{surgeryId}/discharge-draft")).StatusCode);
    }

    /// <summary>U-F3 / I-F3 -- discharge with the defaults snapshots the instructions, sets two follow-ups, enqueues the post-op message and one reminder each (the day before at 10:00 local);
    /// the reminder is delivered in-app, and the customer sees the instructions and follow-ups (US-SRG-07).</summary>
    [Fact]
    public async Task I_F3_Discharge_SnapshotsTheInstructions_QueuesTheMessages_AndTheReminderIsDelivered()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await ReadyForDischargeAsync(world);

        var discharged = await DischargeAsync(world.Nurse, surgeryId);
        Assert.True(discharged.StatusCode == HttpStatusCode.OK, await discharged.Content.ReadAsStringAsync());

        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("Discharged", detail.GetProperty("status").GetString());
        Assert.Contains("indoors", detail.GetProperty("postOpInstructions").GetString());
        var followUps = detail.GetProperty("followUps").EnumerateArray().ToArray();
        Assert.Equal(2, followUps.Length);
        Assert.All(followUps, f => Assert.Equal("Pending", f.GetProperty("status").GetString()));

        Assert.Single(await RequestsAsync(world, NotificationTemplateCatalogue.SurgeryPostOpInstructions, surgeryId));
        var reminders = await RequestsAsync(world, NotificationTemplateCatalogue.SurgeryFollowUpDue, surgeryId);
        Assert.Equal(2, reminders.Length);
        var woundCheck = Assert.Single(reminders, r => r.SubjectRef == followUps[0].GetProperty("id").GetGuid());
        // The wound check is due three days after the procedure; its reminder goes out the day before at 10:00 Dhaka (04:00Z).
        var dueDay = LocalToday(world).AddDays(3);
        Assert.Equal(new DateTimeOffset(dueDay.AddDays(-1).ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(6)), woundCheck.SendAfter);

        world.Clock.SetUtcNow(woundCheck.SendAfter!.Value.AddMinutes(5));
        await RunNotificationDeliveryUntilAsync(world.Api, async db =>
            await db.Set<NotificationAggregate>().AsNoTracking().AnyAsync(n => n.RecipientId == world.CustomerId && n.TemplateKey == NotificationTemplateCatalogue.SurgeryFollowUpDue && n.Channel == NotificationChannel.InApp));

        // The customer's own view.
        var mine = await world.Customer.GetFromJsonAsync<JsonElement>($"/api/customers/me/surgeries?scope=past", Json);
        var row = Assert.Single(mine.GetProperty("items").EnumerateArray());
        Assert.Equal(surgeryId, row.GetProperty("id").GetGuid());
        Assert.Equal("Discharged", row.GetProperty("status").GetString());
        var customerDetail = await world.Customer.GetFromJsonAsync<JsonElement>($"/api/customers/me/surgeries/{surgeryId}", Json);
        Assert.Contains("indoors", customerDetail.GetProperty("postOpInstructions").GetString());
        Assert.Equal(2, customerDetail.GetProperty("followUps").GetArrayLength());
        Assert.Contains("category=FollowUp", customerDetail.GetProperty("bookFollowUpLink").GetString());
    }

    /// <summary>I-F4 -- the wound check is completed before its reminder goes out: the guard suppresses the reminder; the other one is untouched.</summary>
    [Fact]
    public async Task I_F4_CompletingAFollowUp_CancelsItsPendingReminder_WhileTheOpenOneStaysPending()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await ReadyForDischargeAsync(world);
        Assert.Equal(HttpStatusCode.OK, (await DischargeAsync(world.Vet, surgeryId)).StatusCode);

        var followUps = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("followUps").EnumerateArray().ToArray();
        var woundCheckId = followUps[0].GetProperty("id").GetGuid();
        var reminders = await RequestsAsync(world, NotificationTemplateCatalogue.SurgeryFollowUpDue, surgeryId);
        var woundReminder = Assert.Single(reminders, r => r.SubjectRef == woundCheckId);
        var sutureReminder = Assert.Single(reminders, r => r.SubjectRef != woundCheckId);

        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.PostAsync($"/api/staff/surgeries/{surgeryId}/follow-ups/{woundCheckId}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Nurse.PostAsync($"/api/staff/surgeries/{surgeryId}/follow-ups/{woundCheckId}/complete", null)).StatusCode);
        var again = await world.Nurse.PostAsync($"/api/staff/surgeries/{surgeryId}/follow-ups/{woundCheckId}/complete", null);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal([BusinessViolationCodes.FollowUpNotPending], await CodesOfAsync(again));

        world.Clock.SetUtcNow(woundReminder.SendAfter!.Value.AddMinutes(5));
        await RunNotificationDeliveryUntilAsync(world.Api, async db =>
            await db.Set<NotificationRequestAggregate>().AsNoTracking().AnyAsync(r => r.Id == woundReminder.Id && r.Status != NotificationRequestStatus.Pending));

        await using var scope = world.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        Assert.Equal(NotificationRequestStatus.Suppressed, (await db.Set<NotificationRequestAggregate>().AsNoTracking().SingleAsync(r => r.Id == woundReminder.Id)).Status);
        Assert.Equal(NotificationRequestStatus.Pending, (await db.Set<NotificationRequestAggregate>().AsNoTracking().SingleAsync(r => r.Id == sutureReminder.Id)).Status);
        Assert.False(await db.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.RecipientId == world.CustomerId && n.TemplateKey == NotificationTemplateCatalogue.SurgeryFollowUpDue));
    }

    [Fact]
    public async Task ACancelledFollowUp_NeedsAReason_AndAnUnknownOneIsNotFound()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await ReadyForDischargeAsync(world);
        await DischargeAsync(world.Vet, surgeryId);
        var sutureId = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("followUps").EnumerateArray().Last().GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.BadRequest, (await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/follow-ups/{sutureId}/cancel", new { reason = "" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/follow-ups/{Guid.NewGuid()}/cancel", new { reason = "Absorbable sutures" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/follow-ups/{sutureId}/cancel", new { reason = "Absorbable sutures" }, Json)).StatusCode);

        var closed = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("followUps").EnumerateArray().Last();
        Assert.Equal("Cancelled", closed.GetProperty("status").GetString());
    }

    /// <summary>U-F4 (integration) -- a chosen date inside the window is kept; one outside is FollowUpOutsideWindow; an empty list sets none (absorbable sutures).</summary>
    [Fact]
    public async Task Discharge_AcceptsDatesInsideTheWindow_RefusesOutsideIt_AndAnEmptyListSetsNone()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await ReadyForDischargeAsync(world);
        var today = LocalToday(world);

        var outside = await DischargeAsync(world.Vet, surgeryId, new { followUps = new[] { new { kind = "WoundCheck", dueOn = today.AddDays(9) } } });
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.Equal([BusinessViolationCodes.FollowUpOutsideWindow], await CodesOfAsync(outside));
        var unknown = await DischargeAsync(world.Vet, surgeryId, new { followUps = new[] { new { kind = "Recheck", dueOn = today.AddDays(9) } } });
        Assert.Equal([BusinessViolationCodes.FollowUpOutsideWindow], await CodesOfAsync(unknown));
        Assert.Equal("Recovery", (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("status").GetString());

        var ok = await DischargeAsync(world.Vet, surgeryId, new { postOpInstructions = "Soft food only for a week.", followUps = new[] { new { kind = "WoundCheck", dueOn = today.AddDays(4) } } });
        Assert.True(ok.StatusCode == HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("Soft food only for a week.", detail.GetProperty("postOpInstructions").GetString());
        var only = Assert.Single(detail.GetProperty("followUps").EnumerateArray());
        Assert.Equal(today.AddDays(4).ToString("yyyy-MM-dd"), only.GetProperty("dueOn").GetString());
        Assert.Single(await RequestsAsync(world, NotificationTemplateCatalogue.SurgeryFollowUpDue, surgeryId));

        var none = await SeedSurgeryWorldAsync();
        var noFollowUps = await ReadyForDischargeAsync(none);
        Assert.Equal(HttpStatusCode.OK, (await DischargeAsync(none.Vet, noFollowUps, new { followUps = Array.Empty<object>() })).StatusCode);
        Assert.Empty((await GetSurgeryAsync(none.Vet, noFollowUps)).GetProperty("followUps").EnumerateArray());
        Assert.Empty(await RequestsAsync(none, NotificationTemplateCatalogue.SurgeryFollowUpDue, noFollowUps));
        Assert.Single(await RequestsAsync(none, NotificationTemplateCatalogue.SurgeryPostOpInstructions, noFollowUps));
    }

    [Fact]
    public async Task Discharge_IsAClinicalAct_NotAReceptionOne()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await ReadyForDischargeAsync(world);

        Assert.Equal(HttpStatusCode.Forbidden, (await DischargeAsync(world.Reception, surgeryId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await DischargeAsync(world.Admin, surgeryId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DischargeAsync((await SeedSurgeryWorldAsync()).Nurse, surgeryId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await DischargeAsync(world.Nurse, surgeryId)).StatusCode);

        var twice = await DischargeAsync(world.Nurse, surgeryId);
        Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
        Assert.Equal([BusinessViolationCodes.InvalidSurgeryTransition], await CodesOfAsync(twice));
    }

    /// <summary>U-F6 (integration) -- the cat died during recovery: the surgery is closed out from Recovering with no follow-ups, no instructions and no messages.</summary>
    [Fact]
    public async Task ADeceasedCat_IsDischargedFromRecovering_WithNoFollowUpsAndNoMessages()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        await EndAsync(world.Vet, surgeryId);
        await MarkCatDeceasedAsync(world.Admin, world.CatId);

        var response = await DischargeAsync(world.Vet, surgeryId);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("Discharged", detail.GetProperty("status").GetString());
        Assert.Empty(detail.GetProperty("followUps").EnumerateArray());
        Assert.Empty(await RequestsAsync(world, NotificationTemplateCatalogue.SurgeryPostOpInstructions, surgeryId));
        Assert.Empty(await RequestsAsync(world, NotificationTemplateCatalogue.SurgeryFollowUpDue, surgeryId));
    }

    // ── the customer's view and the contracts ──────────────────────────────────────────────────────────────────────────────

    /// <summary>I-F5 -- another customer's surgery is 404 on the portal, exactly like a missing one; the list shows only the caller's own with InCare while the cat is being treated.</summary>
    [Fact]
    public async Task I_F5_ACustomer_SeesOnlyTheirOwnSurgeries()
    {
        var world = await SeedSurgeryWorldAsync();
        var other = await SeedSurgeryWorldAsync();
        var surgeryId = await BookAsync(world, LocalStart(world, 2, 10));

        var upcoming = await world.Customer.GetFromJsonAsync<JsonElement>("/api/customers/me/surgeries", Json);
        var mine = Assert.Single(upcoming.GetProperty("items").EnumerateArray());
        Assert.Equal("Scheduled", mine.GetProperty("status").GetString());
        Assert.Empty((await other.Customer.GetFromJsonAsync<JsonElement>("/api/customers/me/surgeries", Json)).GetProperty("items").EnumerateArray());
        Assert.Empty((await world.Customer.GetFromJsonAsync<JsonElement>("/api/customers/me/surgeries?scope=past", Json)).GetProperty("items").EnumerateArray());

        Assert.Equal(HttpStatusCode.OK, (await world.Customer.GetAsync($"/api/customers/me/surgeries/{surgeryId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Customer.GetAsync($"/api/customers/me/surgeries/{surgeryId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Customer.GetAsync($"/api/customers/me/surgeries/{Guid.NewGuid()}")).StatusCode);

        // In care: PreOp, InTheatre and Recovery all read InCare; the owner sees no instructions before discharge.
        var (inCare, _) = await InTheatreAsync(other);
        await EndAsync(other.Vet, inCare);
        var careRow = Assert.Single((await other.Customer.GetFromJsonAsync<JsonElement>("/api/customers/me/surgeries", Json)).GetProperty("items").EnumerateArray());
        Assert.Equal("InCare", careRow.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, (await other.Customer.GetFromJsonAsync<JsonElement>($"/api/customers/me/surgeries/{inCare}", Json)).GetProperty("postOpInstructions").ValueKind);

        // Staff are not customers.
        Assert.Contains((await world.Reception.GetAsync("/api/customers/me/surgeries")).StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }

    /// <summary>The two cross-module query contracts: the cat's surgery history (EMR) and the chargeable items (BIL, deferred there).</summary>
    [Fact]
    public async Task TheCatHistoryAndChargeableItemsQueries_AnswerTheirContracts()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await ReadyForDischargeAsync(world);

        await using var scope = world.Api.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();

        var history = (await dispatcher.GetResultAsync(new GetCatSurgeriesQuery { CatId = world.CatId, CorrelationId = Guid.NewGuid() })).Result!;
        var item = Assert.Single(history.Items);
        Assert.Equal(surgeryId, item.SurgeryId);
        Assert.True(item.IsSterilisation);
        Assert.Equal("Recovery", item.Status);
        Assert.Equal("None", item.ReportStatus);
        Assert.NotNull(item.VisitId);

        var charge = (await dispatcher.GetResultAsync(new GetSurgeryChargeableItemsQuery { SurgeryId = surgeryId, CorrelationId = Guid.NewGuid() })).Result!;
        Assert.Equal(world.CustomerId, charge.CustomerId);
        Assert.Equal("Standard", charge.PricingBasis);
        Assert.StartsWith("5000", charge.Price.Amount);
        Assert.Empty(charge.Drugs);
        Assert.Null((await dispatcher.GetResultAsync(new GetSurgeryChargeableItemsQuery { SurgeryId = Guid.NewGuid(), CorrelationId = Guid.NewGuid() })).Result);
    }
}

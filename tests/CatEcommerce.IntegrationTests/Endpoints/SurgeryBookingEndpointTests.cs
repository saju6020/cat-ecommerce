using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SRG-001 phase B -- US-SRG-01: book a surgery against a theatre, a surgeon and an assistant; every clash named; races settled by the exclusion constraints; reschedule and
/// cancel through the same checks. Real HTTP; every test builds its own branch (§16.1).</summary>
public sealed class SurgeryBookingEndpointTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    // ── helpers ────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static string AvailabilityUrl(SurgeryWorld world, DateTimeOffset day, Guid? surgeon = null, Guid? assistant = null, Guid? theatre = null, Guid? serviceId = null, Guid? catId = null, Guid? exclude = null)
    {
        var date = DateOnly.FromDateTime(day.ToOffset(TimeSpan.FromHours(6)).DateTime).ToString("yyyy-MM-dd");
        var url = $"/api/staff/surgeries/availability?serviceId={serviceId ?? world.SpayServiceId}&date={date}&surgeonId={surgeon ?? world.VetId}&assistantId={assistant ?? world.AssistantId}&theatreId={theatre ?? world.TheatreId}";
        if (catId is { } cat) url += $"&catId={cat}";
        if (exclude is { } surgeryId) url += $"&excludeSurgeryId={surgeryId}";
        return url;
    }

    private static async Task<DateTimeOffset[]> StartsAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("starts").EnumerateArray().Select(s => s.GetProperty("startsOn").GetDateTimeOffset()).ToArray();
    }

    private async Task<(Guid StaffId, string Mobile, string Password)> AddSurgeonAsync(SurgeryWorld world) => await CreateAndRosterVetAsync(world.Admin, world.BranchId);

    private async Task<Guid> AddAssistantAsync(SurgeryWorld world)
    {
        var nurse = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Nurse);
        await RosterAllWeekAsync(world.Admin, world.BranchId, nurse.StaffId);
        return nurse.StaffId;
    }

    /// <summary>A Nurse-qualified 15-minute service in the consultation room -- an ordinary appointment a nurse performs (a vaccination, say).</summary>
    private static async Task<Guid> CreateNurseServiceAsync(SurgeryWorld world)
    {
        var taxClassId = await CreateTaxClassAsync(world.Admin);
        var create = await world.Admin.PostAsJsonAsync("/api/admin/services", new
        {
            code = NewCode(), name = "Vaccination " + NewCode(), description = "Nurse-led", category = "Vaccination", durationMinutes = 15,
            qualifiedRoleIds = new[] { StaffRoles.Nurse }, requiredRoomKind = "Consultation", taxClassId, isBookableOnline = false
        }, Json);
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var serviceId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("serviceId").GetGuid();
        var priced = await world.Admin.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices", new { amount = "300.00", currencyCode = "BDT", effectiveFrom = world.Clock.GetUtcNow() }, Json);
        Assert.True(priced.StatusCode == HttpStatusCode.Created, await priced.Content.ReadAsStringAsync());
        return serviceId;
    }

    private static Task<HttpResponseMessage> BookOrdinaryAsync(SurgeryWorld world, Guid serviceId, Guid performerId, DateTimeOffset startsOn, Guid? catId = null) =>
        world.Admin.PostAsJsonAsync("/api/staff/appointments", new
        { customerId = world.CustomerId, catId = catId ?? world.CatId, serviceId, startsOn, performerId, roomId = world.Pharmacy.Clinic.Context.RoomId }, Json);

    // ── I-B1 ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-B1 -- after a booking, the start is gone for that theatre, that surgeon and that assistant (each alone, with the others free).</summary>
    [Fact]
    public async Task I_B1_ABookedStart_DisappearsFromAvailability_ForTheTheatre_TheSurgeon_AndTheAssistant()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 1, 10);
        var otherSurgeon = await AddSurgeonAsync(world);
        var otherAssistant = await AddAssistantAsync(world);
        var otherTheatre = await CreateRoomAsync(world.Admin, world.BranchId, "Theatre");

        Assert.Contains(start, await StartsAsync(world.Reception, AvailabilityUrl(world, start)));

        await BookAsync(world, start);

        Assert.DoesNotContain(start, await StartsAsync(world.Reception, AvailabilityUrl(world, start)));
        // Each resource alone is enough to take the start away: the same theatre with a free surgeon and a free assistant...
        Assert.DoesNotContain(start, await StartsAsync(world.Reception, AvailabilityUrl(world, start, surgeon: otherSurgeon.StaffId, assistant: otherAssistant)));
        // ...the same surgeon with a free theatre and a free assistant...
        Assert.DoesNotContain(start, await StartsAsync(world.Reception, AvailabilityUrl(world, start, assistant: otherAssistant, theatre: otherTheatre)));
        // ...the same assistant with a free theatre and a free surgeon.
        Assert.DoesNotContain(start, await StartsAsync(world.Reception, AvailabilityUrl(world, start, surgeon: otherSurgeon.StaffId, theatre: otherTheatre)));
        // A wholly free trio still has it.
        Assert.Contains(start, await StartsAsync(world.Reception, AvailabilityUrl(world, start, otherSurgeon.StaffId, otherAssistant, otherTheatre)));
    }

    // ── I-B2 ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-B2 -- US-SRG-01: the theatre busy, the surgeon busy and the assistant busy are each refused with their OWN reason (seeded one at a time), and all together with one
    /// violation each, naming the conflicting time.</summary>
    [Fact]
    public async Task I_B2_EachClash_IsNamedOnItsOwn_AndAllTogether_OneViolationEach()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 1, 10);
        await BookAsync(world, start);
        var surgeon2 = (await AddSurgeonAsync(world)).StaffId;
        var assistant2 = await AddAssistantAsync(world);
        var theatre2 = await CreateRoomAsync(world.Admin, world.BranchId, "Theatre");
        var cat2 = await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Pixel");

        async Task<string[]> RefusedCodesAsync(Guid theatre, Guid surgeon, Guid assistant)
        {
            var response = await TryBookAsync(world.Reception, BookBody(world, start, theatreId: theatre, surgeonId: surgeon, assistantId: assistant, catId: cat2));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return await CodesOfAsync(response);
        }

        Assert.Equal([BusinessViolationCodes.TheatreBusy], await RefusedCodesAsync(world.TheatreId, surgeon2, assistant2));
        Assert.Equal([BusinessViolationCodes.SurgeonBusy], await RefusedCodesAsync(theatre2, world.VetId, assistant2));
        Assert.Equal([BusinessViolationCodes.AssistantBusy], await RefusedCodesAsync(theatre2, surgeon2, world.AssistantId));

        var all = await TryBookAsync(world.Reception, BookBody(world, start, catId: cat2));
        Assert.Equal(HttpStatusCode.BadRequest, all.StatusCode);
        var errors = await ErrorsOfAsync(all);
        Assert.Equal(3, errors.Length);
        Assert.Equal([BusinessViolationCodes.TheatreBusy, BusinessViolationCodes.SurgeonBusy, BusinessViolationCodes.AssistantBusy], errors.Select(e => e.GetProperty("errorCode").GetString()).ToArray());
        Assert.Equal(["theatreId", "surgeonId", "assistantId"], errors.Select(e => e.GetProperty("propertyName").GetString()).ToArray());
        Assert.All(errors, e => Assert.Contains("10:00–11:00", e.GetProperty("errorMessage").GetString()));
    }

    /// <summary>I-B2 (rest of the checks of §8.5): qualification, the same person twice, the past, the grid, the cat's own double booking.</summary>
    [Fact]
    public async Task Booking_RefusesUnqualifiedStaff_APastTime_AnOffGridTime_AndTheSameCatTwice()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 1, 10);

        var nurseAsSurgeon = await TryBookAsync(world.Reception, BookBody(world, start, surgeonId: world.AssistantId, assistantId: world.NurseId));
        Assert.Contains(BusinessViolationCodes.SurgeonNotQualified, await CodesOfAsync(nurseAsSurgeon));

        var receptionAssistant = await TryBookAsync(world.Reception, BookBody(world, start, assistantId: (await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Reception)).StaffId));
        Assert.Contains(BusinessViolationCodes.AssistantNotQualified, await CodesOfAsync(receptionAssistant));

        var same = await TryBookAsync(world.Reception, BookBody(world, start, assistantId: world.VetId));
        Assert.Contains(BusinessViolationCodes.AssistantSameAsSurgeon, await CodesOfAsync(same));

        var past = await TryBookAsync(world.Reception, BookBody(world, world.Clock.GetUtcNow().AddHours(-1)));
        Assert.Contains(BusinessViolationCodes.SlotInPast, await CodesOfAsync(past));

        var offGrid = await TryBookAsync(world.Reception, BookBody(world, start.AddMinutes(7)));
        Assert.Contains(BusinessViolationCodes.SlotNotOnGrid, await CodesOfAsync(offGrid));

        var notATheatre = await TryBookAsync(world.Reception, BookBody(world, start, theatreId: world.Pharmacy.Clinic.Context.RoomId));
        Assert.Contains(BusinessViolationCodes.TheatreNotAvailable, await CodesOfAsync(notATheatre));

        await BookAsync(world, start);
        var catTwice = await TryBookAsync(world.Reception, BookBody(world, start.AddMinutes(30), theatreId: await CreateRoomAsync(world.Admin, world.BranchId, "Theatre"),
            surgeonId: (await AddSurgeonAsync(world)).StaffId, assistantId: await AddAssistantAsync(world)));
        Assert.Equal([BusinessViolationCodes.CatAlreadyBooked], await CodesOfAsync(catTwice));
    }

    // ── I-B3 ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-B3 -- the assistant is busy as the PERFORMER of an ordinary appointment (refused AssistantBusy); and reversed: an ordinary staff booking of a nurse who is assisting
    /// is refused PerformerBusy (the staff-time rows see both roles).</summary>
    [Fact]
    public async Task I_B3_AnAssistantIsBusy_AsAPerformer_AndAPerformerIsBusy_AsAnAssistant()
    {
        var world = await SeedSurgeryWorldAsync();
        var nurseService = await CreateNurseServiceAsync(world);
        var ten = LocalStart(world, 1, 10);

        var vaccinationCat = await SeedCatOfAsync(world.CustomerId, CatSex.Male, name: "Vax");
        var vaccination = await BookOrdinaryAsync(world, nurseService, world.AssistantId, ten, vaccinationCat);
        Assert.True(vaccination.StatusCode == HttpStatusCode.Created, await vaccination.Content.ReadAsStringAsync());

        var refused = await TryBookAsync(world.Reception, BookBody(world, ten));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal([BusinessViolationCodes.AssistantBusy], await CodesOfAsync(refused));

        // Reversed: a surgery at 14:00 with the nurse assisting; the nurse cannot be booked as a performer at 14:15.
        var afternoon = LocalStart(world, 1, 14);
        await BookAsync(world, afternoon);
        var reverse = await BookOrdinaryAsync(world, nurseService, world.AssistantId, afternoon.AddMinutes(15), catId: await SeedCatOfAsync(world.CustomerId, CatSex.Male, name: "Rex"));
        Assert.Equal(HttpStatusCode.BadRequest, reverse.StatusCode);
        Assert.Contains(BusinessViolationCodes.PerformerBusy, await reverse.Content.ReadAsStringAsync());
    }

    // ── I-B4 / I-B5 ───────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-B4 -- ADR-068: 20 parallel bookings of the same theatre and time with DIFFERENT surgeons, assistants and cats: exactly one is created; every other is refused
    /// TheatreBusy (400 when the pre-check saw the winner, 409 when the exclusion constraint settled the race). Never a retry, never a delay.</summary>
    [Fact]
    public async Task I_B4_TwentyParallelBookings_OfOneTheatre_ExactlyOneWins()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 1, 10);
        const int Contenders = 20;

        var surgeons = await Task.WhenAll(Enumerable.Range(0, Contenders).Select(_ => AddSurgeonAsync(world)));
        var assistants = await Task.WhenAll(Enumerable.Range(0, Contenders).Select(_ => AddAssistantAsync(world)));
        var cats = new List<Guid>();
        for (var i = 0; i < Contenders; i++) cats.Add(await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Cat" + i));

        using var gate = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, Contenders).Select(i => Task.Run(async () =>
        {
            gate.Wait();
            return await TryBookAsync(world.Reception, BookBody(world, start, surgeonId: surgeons[i].StaffId, assistantId: assistants[i], catId: cats[i]));
        })).ToList();
        gate.Set();
        var responses = await Task.WhenAll(tasks);

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(1, created);
        foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            Assert.True(refused.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict, $"{(int)refused.StatusCode}");
            Assert.Contains(BusinessViolationCodes.TheatreBusy, await refused.Content.ReadAsStringAsync());
        }

        Assert.Equal(1, await CountAsync("SELECT count(*) FROM clinic.surgery WHERE branch_id = @b AND starts_on = @s", ("b", world.BranchId), ("s", start.ToUniversalTime())));
    }

    /// <summary>I-B5 -- ADR-159: a surgery booking with assistant N racing an ordinary staff booking with performer N at the same moment: exactly one succeeds, the other is refused
    /// (StaffBusy / PerformerBusy), repeated across several independent moments.</summary>
    [Fact]
    public async Task I_B5_ASurgery_AndAnOrdinaryBooking_RacingForTheSameNurse_ExactlyOneWins()
    {
        var world = await SeedSurgeryWorldAsync();
        var nurseService = await CreateNurseServiceAsync(world);

        for (var round = 0; round < 6; round++)
        {
            var start = LocalStart(world, 1, 8 + round);
            var surgeon = (await AddSurgeonAsync(world)).StaffId;
            var theatre = await CreateRoomAsync(world.Admin, world.BranchId, "Theatre");
            var surgeryCat = await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "A" + round);
            var visitCat = await SeedCatOfAsync(world.CustomerId, CatSex.Male, name: "B" + round);

            using var gate = new ManualResetEventSlim(false);
            var surgeryTask = Task.Run(async () => { gate.Wait(); return await TryBookAsync(world.Reception, BookBody(world, start, theatreId: theatre, surgeonId: surgeon, catId: surgeryCat)); });
            var visitTask = Task.Run(async () => { gate.Wait(); return await BookOrdinaryAsync(world, nurseService, world.AssistantId, start, visitCat); });
            gate.Set();
            var (surgery, visit) = (await surgeryTask, await visitTask);

            var succeeded = new[] { surgery, visit }.Count(r => r.StatusCode == HttpStatusCode.Created);
            Assert.True(succeeded == 1, $"round {round}: surgery {(int)surgery.StatusCode}, visit {(int)visit.StatusCode}");

            var loser = surgery.StatusCode == HttpStatusCode.Created ? visit : surgery;
            Assert.True(loser.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
            var text = await loser.Content.ReadAsStringAsync();
            Assert.True(text.Contains(BusinessViolationCodes.StaffBusy) || text.Contains(BusinessViolationCodes.PerformerBusy) || text.Contains(BusinessViolationCodes.AssistantBusy)
                        || text.Contains(BusinessViolationCodes.SlotNoLongerAvailable), text);
        }
    }

    // ── I-B6 / I-B7 ───────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-B6 -- HO-APT-001 cannot change a surgery's appointment behind the surgery's back: move, cancel, check-in, triage, start, complete and no-show are refused
    /// AppointmentManagedBySurgery, and so are the customer's own reschedule and cancel.</summary>
    [Fact]
    public async Task I_B6_EveryHoAptChange_OfASurgeryAppointment_IsRefused()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await BookAsync(world, LocalStart(world, 1, 10));
        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        var appointmentId = detail.GetProperty("appointmentId").GetGuid();
        var staff = world.Admin;

        async Task AssertManagedAsync(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(BusinessViolationCodes.AppointmentManagedBySurgery, await response.Content.ReadAsStringAsync());
        }

        await AssertManagedAsync(await staff.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/move", new { expectedVersion = 0, startsOn = LocalStart(world, 1, 12), performerId = world.VetId, roomId = world.TheatreId }, Json));
        await AssertManagedAsync(await staff.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/cancel", new { reason = "Trying to cancel behind the surgery" }, Json));
        await AssertManagedAsync(await staff.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/check-in", new { }, Json));
        await AssertManagedAsync(await staff.PostAsync($"/api/staff/appointments/{appointmentId}/no-show", null));
        await AssertManagedAsync(await world.Customer.PostAsJsonAsync($"/api/customers/me/appointments/{appointmentId}/cancel", new { }, Json));
        await AssertManagedAsync(await world.Customer.PostAsJsonAsync($"/api/customers/me/appointments/{appointmentId}/reschedule", new { startsOn = LocalStart(world, 1, 12), performerId = world.VetId }, Json));

        Assert.Equal(AppointmentStatus.Booked.Value, await ScalarAsync<int>("SELECT status FROM clinic.appointment WHERE \"Id\" = @a", ("a", appointmentId)));
    }

    /// <summary>I-B7 -- ADR-172: a theatre service is not bookable as an ordinary appointment, online or by staff.</summary>
    [Fact]
    public async Task I_B7_ATheatreService_IsNotBookable_AsAnOrdinaryAppointment()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 1, 10);

        var online = await world.Customer.PostAsJsonAsync("/api/customers/me/appointment-holds",
            new { branchId = world.BranchId, serviceId = world.SpayServiceId, catId = world.CatId, startsOn = start, performerId = world.VetId }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, online.StatusCode);
        Assert.Contains(BusinessViolationCodes.ServiceNotBookable, await online.Content.ReadAsStringAsync());

        var byStaff = await BookOrdinaryAsync(world, world.SpayServiceId, world.VetId, start);
        Assert.Equal(HttpStatusCode.BadRequest, byStaff.StatusCode);
        Assert.Contains(BusinessViolationCodes.ServiceNotBookable, await byStaff.Content.ReadAsStringAsync());
    }

    // ── I-B8 .. I-B12 ────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-B8 -- the booking commits the appointment (Booked, with the surgery id), the surgery and the messages together; the surgery carries a price SNAPSHOT and nothing in
    /// the response is a deposit, payment, balance or invoice (ADR-170).</summary>
    [Fact]
    public async Task I_B8_TheBooking_CommitsTheAppointment_TheSurgery_AndTheMessages_WithNoPaymentAnywhere()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 2, 10);

        var response = await TryBookAsync(world.Reception, BookBody(world, start));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var surgeryId = JsonDocument.Parse(raw).RootElement.GetProperty("surgeryId").GetGuid();

        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        var appointmentId = detail.GetProperty("appointmentId").GetGuid();
        Assert.Equal("Scheduled", detail.GetProperty("status").GetString());
        Assert.Equal("Standard", detail.GetProperty("priceSnapshot").GetProperty("basis").GetString());
        Assert.Equal("5000.00", detail.GetProperty("priceSnapshot").GetProperty("amount").GetProperty("amount").GetString());
        Assert.Equal(5, detail.GetProperty("checklist").GetArrayLength());

        // One transaction: the appointment is Booked, belongs to the surgery, holds the surgeon and the assistant.
        Assert.Equal(AppointmentStatus.Booked.Value, await ScalarAsync<int>("SELECT status FROM clinic.appointment WHERE \"Id\" = @a", ("a", appointmentId)));
        Assert.Equal(surgeryId, await ScalarAsync<Guid>("SELECT surgery_id FROM clinic.appointment WHERE \"Id\" = @a", ("a", appointmentId)));
        Assert.Equal(2, await CountAsync("SELECT count(*) FROM clinic.appointment_staff_time WHERE appointment_id = @a AND occupies", ("a", appointmentId)));
        Assert.Equal(BookingChannel.Surgery.Value, await ScalarAsync<int>("SELECT channel FROM clinic.appointment WHERE \"Id\" = @a", ("a", appointmentId)));

        // The booked message and the pre-op reminder were enlisted with the booking.
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM notify.notification_request WHERE recipient_id = @c AND template_key = 'surgery.booked'", ("c", world.CustomerId)));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM notify.notification_request WHERE recipient_id = @c AND template_key = 'surgery.pre-op-reminder' AND subject_id = @s", ("c", world.CustomerId), ("s", surgeryId)));

        // ADR-170: no deposit, payment, balance or invoice anywhere in what the API returned.
        var everything = (raw + detail.GetRawText()).ToLowerInvariant();
        foreach (var forbidden in new[] { "deposit", "payment", "balance", "invoice", "owing", "amountdue" })
            Assert.DoesNotContain(forbidden, everything);
    }

    /// <summary>I-B11 -- the calendar shows the surgery on the theatre and on the assistant (and the surgeon), with its surgery id; it is not in the HO-APT day list, the queue or
    /// the customer's appointment list.</summary>
    [Fact]
    public async Task I_B11_ASurgery_IsOnTheCalendar_ButNotInTheDayListTheQueueOrTheCustomersList()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 1, 10);
        var surgeryId = await BookAsync(world, start);
        var date = DateOnly.FromDateTime(start.ToOffset(TimeSpan.FromHours(6)).DateTime).ToString("yyyy-MM-dd");

        foreach (var (view, id) in new[] { ("room", world.TheatreId), ("staff", world.AssistantId), ("staff", world.VetId) })
        {
            var calendar = await world.Admin.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/calendar?from={date}&to={date}&view={view}&ids={id}", Json);
            var entry = Assert.Single(calendar.GetProperty("appointments").EnumerateArray(), a => a.TryGetProperty("surgeryId", out var sid) && sid.ValueKind == JsonValueKind.String && sid.GetGuid() == surgeryId);
            Assert.Equal(world.AssistantId, entry.GetProperty("assistantId").GetGuid());
        }

        var dayList = await world.Admin.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/day?date={date}", Json);
        Assert.DoesNotContain(dayList.EnumerateArray(), a => a.GetProperty("performerId").ValueKind == JsonValueKind.String && a.GetProperty("performerId").GetGuid() == world.VetId && a.GetProperty("serviceName").GetString()!.StartsWith("Spay"));

        var queue = await world.Admin.GetAsync("/api/staff/queue");
        Assert.DoesNotContain(surgeryId.ToString(), await queue.Content.ReadAsStringAsync());

        var mine = await world.Customer.GetAsync("/api/customers/me/appointments?scope=upcoming");
        Assert.DoesNotContain("Spay", await mine.Content.ReadAsStringAsync());
    }

    // ── cat checks (U-B6, U-B7) ─────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>U-B6 -- a spay for a male cat and a neuter for a female are refused ProcedureSexMismatch; an Unknown sex passes.</summary>
    [Fact]
    public async Task U_B6_TheProcedureMustFitTheCatsSex_UnknownPasses()
    {
        var world = await SeedSurgeryWorldAsync();
        var male = await SeedCatOfAsync(world.CustomerId, CatSex.Male, name: "Tom");
        var female = await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Tilly");
        var unknown = await SeedCatOfAsync(world.CustomerId, CatSex.Unknown, name: "Mystery");

        var spayMale = await TryBookAsync(world.Reception, BookBody(world, LocalStart(world, 1, 9), serviceId: world.SpayServiceId, catId: male));
        Assert.Contains(BusinessViolationCodes.ProcedureSexMismatch, await CodesOfAsync(spayMale));

        var neuterFemale = await TryBookAsync(world.Reception, BookBody(world, LocalStart(world, 1, 9), serviceId: world.NeuterServiceId, catId: female));
        Assert.Contains(BusinessViolationCodes.ProcedureSexMismatch, await CodesOfAsync(neuterFemale));

        await BookAsync(world, LocalStart(world, 1, 9), world.SpayServiceId, unknown);
        await BookAsync(world, LocalStart(world, 1, 12), world.NeuterServiceId, unknown);
        await BookAsync(world, LocalStart(world, 1, 15), world.NeuterServiceId, male);
        await BookAsync(world, LocalStart(world, 1, 17), world.SpayServiceId, female);
    }

    /// <summary>U-B7 -- a sterilisation of a cat recorded Neutered needs the vet's confirmation that the record may be wrong; a dental on the same cat does not.</summary>
    [Fact]
    public async Task U_B7_ASterilisation_OfANeuteredCat_NeedsConfirmation()
    {
        var world = await SeedSurgeryWorldAsync();
        var neutered = await SeedCatOfAsync(world.CustomerId, CatSex.Female, neutered: true, name: "Fixed");
        var start = LocalStart(world, 1, 10);

        var refused = await TryBookAsync(world.Reception, BookBody(world, start, catId: neutered));
        Assert.Contains(BusinessViolationCodes.CatAlreadyNeutered, await CodesOfAsync(refused));

        var confirmed = await TryBookAsync(world.Reception, BookBody(world, start, catId: neutered, confirmNeuterDiscrepancy: true));
        Assert.Equal(HttpStatusCode.Created, confirmed.StatusCode);

        await BookAsync(world, LocalStart(world, 1, 14), world.DentalServiceId, neutered);
    }

    [Fact]
    public async Task Booking_RefusesAServiceThatIsNotATheatreProcedure_AnotherCustomersCat_AndEveryRoleButSchedulers()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 1, 10);

        var consultation = await TryBookAsync(world.Reception, BookBody(world, start, serviceId: world.Pharmacy.Clinic.ServiceId));
        Assert.Equal(HttpStatusCode.BadRequest, consultation.StatusCode);
        Assert.Contains(BusinessViolationCodes.ServiceNotSurgical, await consultation.Content.ReadAsStringAsync());

        var strangerCat = await SeedCatOfAsync((await RegisterAndSignInCustomerOnAsync(world.Api)).CustomerId, CatSex.Female, name: "Stray");
        var notTheirs = await TryBookAsync(world.Reception, BookBody(world, start, catId: strangerCat));
        Assert.Contains(BusinessViolationCodes.CatNotBookable, await CodesOfAsync(notTheirs));

        Assert.Equal(HttpStatusCode.Forbidden, (await TryBookAsync(world.Nurse, BookBody(world, start))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await TryBookAsync(world.Vet, BookBody(world, start))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await TryBookAsync(world.Admin, BookBody(world, start.AddHours(3), theatreId: await CreateRoomAsync(world.Admin, world.BranchId, "Theatre"),
            surgeonId: (await AddSurgeonAsync(world)).StaffId, assistantId: await AddAssistantAsync(world), catId: await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Lulu")))).StatusCode);
    }

    /// <summary>U-B5 -- a LAPSED hold overlapping the surgeon is ignored (and expired) rather than blocking the booking.</summary>
    [Fact]
    public async Task U_B5_ALapsedHold_OnTheSurgeon_DoesNotBlockABooking()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 0, 11);

        var hold = await world.Customer.PostAsJsonAsync("/api/customers/me/appointment-holds",
            new { branchId = world.BranchId, serviceId = world.Pharmacy.Clinic.ServiceId, catId = world.CatId, startsOn = start, performerId = world.VetId }, Json);
        Assert.True(hold.StatusCode == HttpStatusCode.Created, await hold.Content.ReadAsStringAsync());
        var holdId = (await hold.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("appointmentId").GetGuid();

        world.Clock.Advance(TimeSpan.FromMinutes(11)); // the default hold is 10 minutes
        var otherCat = await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Rani");
        var response = await TryBookAsync(world.Reception, BookBody(world, start, catId: otherCat));

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        Assert.Equal(AppointmentStatus.HoldExpired.Value, await ScalarAsync<int>("SELECT status FROM clinic.appointment WHERE \"Id\" = @a", ("a", holdId)));
    }

    // ── reschedule and cancel ───────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reschedule_RunsTheSameChecks_ExcludesItsOwnAppointment_AndMovesTheAvailability()
    {
        var world = await SeedSurgeryWorldAsync();
        var ten = LocalStart(world, 1, 10);
        var surgeryId = await BookAsync(world, ten);
        var version = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("version").GetInt32();

        // Overlapping its OWN old time is fine (the surgery's appointment is excluded).
        var quarterPast = ten.AddMinutes(15);
        var moved = await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/reschedule",
            new { expectedVersion = version, startsOn = quarterPast, theatreId = world.TheatreId, surgeonId = world.VetId, assistantId = world.AssistantId }, Json);
        Assert.True(moved.StatusCode == HttpStatusCode.OK, await moved.Content.ReadAsStringAsync());
        var body = await moved.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(quarterPast, body.GetProperty("surgery").GetProperty("startsOn").GetDateTimeOffset());
        var storedStart = await ScalarAsync<DateTime>("SELECT starts_on FROM clinic.appointment WHERE \"Id\" = @a", ("a", body.GetProperty("surgery").GetProperty("appointmentId").GetGuid()));
        Assert.Equal(quarterPast.UtcDateTime, DateTime.SpecifyKind(storedStart, DateTimeKind.Utc));

        // Seen from the surgery itself (excludeSurgeryId) its own time is free; seen by anyone else it is taken.
        Assert.Contains(quarterPast, await StartsAsync(world.Reception, AvailabilityUrl(world, ten, exclude: surgeryId)));
        Assert.DoesNotContain(quarterPast, await StartsAsync(world.Reception, AvailabilityUrl(world, ten)));

        // Moved to the afternoon, the morning start it left is free again and the afternoon one is not.
        var afternoon = LocalStart(world, 1, 14);
        var version2 = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("version").GetInt32();
        var second = await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/reschedule",
            new { expectedVersion = version2, startsOn = afternoon, theatreId = world.TheatreId, surgeonId = world.VetId, assistantId = world.AssistantId }, Json);
        Assert.True(second.StatusCode == HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
        var starts = await StartsAsync(world.Reception, AvailabilityUrl(world, ten));
        Assert.Contains(ten, starts);
        Assert.Contains(quarterPast, starts);
        Assert.DoesNotContain(afternoon, starts);

        // A stale version is a 409.
        var stale = await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/reschedule",
            new { expectedVersion = version, startsOn = ten, theatreId = world.TheatreId, surgeonId = world.VetId, assistantId = world.AssistantId }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Reschedule_IntoAnotherBookingsTime_NamesTheClash_AndOnlyAScheduledSurgeryCanMove()
    {
        var world = await SeedSurgeryWorldAsync();
        var ten = LocalStart(world, 1, 10);
        var noon = LocalStart(world, 1, 12);
        await BookAsync(world, noon, catId: await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Other"));
        var surgeryId = await BookAsync(world, ten);
        var version = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("version").GetInt32();

        var clash = await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/reschedule",
            new { expectedVersion = version, startsOn = noon, theatreId = world.TheatreId, surgeonId = world.VetId, assistantId = world.AssistantId }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, clash.StatusCode);
        Assert.Equal([BusinessViolationCodes.TheatreBusy, BusinessViolationCodes.SurgeonBusy, BusinessViolationCodes.AssistantBusy], await CodesOfAsync(clash));

        var cancel = await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/cancel", new { reason = "Owner called" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        var afterCancel = await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/reschedule",
            new { expectedVersion = version + 1, startsOn = ten.AddHours(1), theatreId = world.TheatreId, surgeonId = world.VetId, assistantId = world.AssistantId }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, afterCancel.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvalidSurgeryTransition, await afterCancel.Content.ReadAsStringAsync());
    }

    /// <summary>I-B10 (state half) -- cancel: the surgery and its appointment are Cancelled, the slot is free for a new booking, a reason is required.</summary>
    [Fact]
    public async Task I_B10_Cancel_FreesTheSlot_AndNeedsAReason()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 1, 10);
        var surgeryId = await BookAsync(world, start);
        var appointmentId = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("appointmentId").GetGuid();

        var noReason = await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/cancel", new { reason = " " }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/cancel", new { reason = "Owner called to cancel" }, Json)).StatusCode);

        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("Cancelled", detail.GetProperty("status").GetString());
        Assert.Equal("Owner called to cancel", detail.GetProperty("cancellationReason").GetString());
        Assert.Equal(AppointmentStatus.Cancelled.Value, await ScalarAsync<int>("SELECT status FROM clinic.appointment WHERE \"Id\" = @a", ("a", appointmentId)));
        Assert.Contains(start, await StartsAsync(world.Reception, AvailabilityUrl(world, start)));
        await BookAsync(world, start); // the same trio, the same time, again
    }

    /// <summary>I-B12 -- approving leave for the ASSISTANT flags the surgery's appointment; the day list shows needsRescheduling.</summary>
    [Fact]
    public async Task I_B12_ApprovedLeave_OfTheAssistant_FlagsTheSurgery()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 3, 10);
        var surgeryId = await BookAsync(world, start);
        var localDate = DateOnly.FromDateTime(start.ToOffset(TimeSpan.FromHours(6)).DateTime);

        // The leave endpoints are HO-ADM-001's: an administrator requests it on the assistant's behalf and approves it.
        var create = await world.Admin.PostAsJsonAsync("/api/admin/leave-requests", new { staffAccountId = world.AssistantId, startsOn = localDate, endsOn = localDate, kind = "Annual", note = "Family event" }, Json);
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var leaveId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("leaveRequestId").GetGuid();
        var approve = await world.Admin.PostAsJsonAsync($"/api/admin/leave-requests/{leaveId}/approve", new { }, Json);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var day = await world.Reception.GetFromJsonAsync<JsonElement>($"/api/staff/surgeries/day?date={localDate:yyyy-MM-dd}", Json);
        var row = Assert.Single(day.GetProperty("surgeries").EnumerateArray(), s => s.GetProperty("surgeryId").GetGuid() == surgeryId);
        Assert.True(row.GetProperty("needsRescheduling").GetBoolean());
    }

    /// <summary>Another branch's surgery is the same 404 as a surgery that does not exist (HO-ADM-001 §8.2).</summary>
    [Fact]
    public async Task ASurgery_OfAnotherBranch_IsNotFound_AndEveryByIdActionAgrees()
    {
        var world = await SeedSurgeryWorldAsync();
        var other = await SeedSurgeryWorldAsync();
        var surgeryId = await BookAsync(world, LocalStart(world, 1, 10));

        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.GetAsync($"/api/staff/surgeries/{surgeryId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/cancel", new { reason = "Not mine to cancel" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Reception.PostAsync($"/api/staff/surgeries/{surgeryId}/admit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Vet.PostAsync($"/api/staff/surgeries/{surgeryId}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.GetAsync($"/api/staff/surgeries/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.Admin.GetAsync($"/api/staff/surgeries/{surgeryId}")).StatusCode);
    }

    [Fact]
    public async Task TheServicesAndStaffLists_OfferWhatABookingFormNeeds()
    {
        var world = await SeedSurgeryWorldAsync();

        var services = await world.Reception.GetFromJsonAsync<JsonElement>("/api/staff/surgeries/services", Json);
        var spay = Assert.Single(services.EnumerateArray(), s => s.GetProperty("id").GetGuid() == world.SpayServiceId);
        Assert.True(spay.GetProperty("isSterilisation").GetBoolean());
        Assert.Equal(60, spay.GetProperty("durationMinutes").GetInt32());
        Assert.Equal("5000.00", spay.GetProperty("price").GetProperty("amount").GetString());
        var dental = Assert.Single(services.EnumerateArray(), s => s.GetProperty("id").GetGuid() == world.DentalServiceId);
        Assert.False(dental.GetProperty("isSterilisation").GetBoolean());
        Assert.DoesNotContain(services.EnumerateArray(), s => s.GetProperty("id").GetGuid() == world.Pharmacy.Clinic.ServiceId); // a consultation is not a theatre service

        var staff = await world.Reception.GetFromJsonAsync<JsonElement>($"/api/staff/surgeries/staff?serviceId={world.SpayServiceId}", Json);
        Assert.Contains(staff.GetProperty("surgeons").EnumerateArray(), s => s.GetProperty("id").GetGuid() == world.VetId);
        Assert.DoesNotContain(staff.GetProperty("surgeons").EnumerateArray(), s => s.GetProperty("id").GetGuid() == world.AssistantId);
        Assert.Contains(staff.GetProperty("assistants").EnumerateArray(), s => s.GetProperty("id").GetGuid() == world.AssistantId);
        Assert.Contains(staff.GetProperty("assistants").EnumerateArray(), s => s.GetProperty("id").GetGuid() == world.VetId);
    }

    // ── TEST gate (2026-10-11): the other two exclusion constraints ───────────────────────────────────────────────────────

    private async Task<HttpResponseMessage[]> RaceAsync(int contenders, Func<int, Task<HttpResponseMessage>> book)
    {
        using var gate = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, contenders).Select(i => Task.Run(async () => { gate.Wait(); return await book(i); })).ToList();
        gate.Set();
        return await Task.WhenAll(tasks);
    }

    /// <summary>T-27 -- 20 parallel bookings of ONE surgeon at the same time, each in its own theatre with its own assistant and cat: exactly one is created, every other is refused SurgeonBusy (400 from the
    /// pre-check or 409 from the performer exclusion constraint), one surgery row.</summary>
    [Fact]
    public async Task TwentyParallelBookings_OfOneSurgeon_ExactlyOneWins()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 1, 11);
        const int Contenders = 20;
        var theatres = new List<Guid>();
        var assistants = new List<Guid>();
        var cats = new List<Guid>();
        for (var i = 0; i < Contenders; i++)
        {
            theatres.Add(await CreateRoomAsync(world.Admin, world.BranchId, "Theatre"));
            assistants.Add(await AddAssistantAsync(world));
            cats.Add(await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "S" + i));
        }

        var responses = await RaceAsync(Contenders, i => TryBookAsync(world.Reception, BookBody(world, start, theatreId: theatres[i], assistantId: assistants[i], catId: cats[i])));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            Assert.True(refused.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict, $"{(int)refused.StatusCode}");
            Assert.Contains(BusinessViolationCodes.SurgeonBusy, await refused.Content.ReadAsStringAsync());
        }

        Assert.Equal(1, await CountAsync("SELECT count(*) FROM clinic.surgery WHERE branch_id = @b AND starts_on = @s", ("b", world.BranchId), ("s", start.ToUniversalTime())));
    }

    /// <summary>T-28 -- 20 parallel SURGERY bookings of ONE assistant at the same time (distinct theatres, surgeons, cats): exactly one is created, the rest are refused AssistantBusy, one surgery row.</summary>
    [Fact]
    public async Task TwentyParallelBookings_OfOneAssistant_ExactlyOneWins()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 1, 12);
        const int Contenders = 20;
        var theatres = new List<Guid>();
        var surgeons = new List<Guid>();
        var cats = new List<Guid>();
        for (var i = 0; i < Contenders; i++)
        {
            theatres.Add(await CreateRoomAsync(world.Admin, world.BranchId, "Theatre"));
            surgeons.Add((await AddSurgeonAsync(world)).StaffId);
            cats.Add(await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "A" + i));
        }

        var responses = await RaceAsync(Contenders, i => TryBookAsync(world.Reception, BookBody(world, start, theatreId: theatres[i], surgeonId: surgeons[i], catId: cats[i])));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            Assert.True(refused.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict, $"{(int)refused.StatusCode}");
            var text = await refused.Content.ReadAsStringAsync();
            Assert.True(text.Contains(BusinessViolationCodes.AssistantBusy) || text.Contains(BusinessViolationCodes.StaffBusy) || text.Contains("SlotNoLongerAvailable"), text); // a race loser can surface the generic slot code (backlog SRG 46)
        }

        Assert.Equal(1, await CountAsync("SELECT count(*) FROM clinic.surgery WHERE branch_id = @b AND starts_on = @s", ("b", world.BranchId), ("s", start.ToUniversalTime())));
    }
}

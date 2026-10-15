using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Vaccination;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-BRD-001 section 16.1 -- every test creates its own branch with its own units, rate cards, routine and rostered caretakers, and mints its own customers and
/// cats; assertions are scoped to that branch. The exclusion constraints are real and a test never disables them.
/// </summary>
public abstract class BoardingIntegrationTestBase(BoardingApiFactory factory) : CatEcommerceIntegrationTestBase<BoardingApiFactory>(factory)
{
    protected override HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        HandleCookies = true
    });

    protected DateTimeOffset Now => Factory.Clock.GetUtcNow();

    protected static string NewMobile() => "01" + Random.Shared.NextInt64(100_000_000, 999_999_999);
    protected static string NewCode() => "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    protected static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        var header = setCookies.First(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(header[(cookieName.Length + 1)..].Split(';')[0]);
    }

    // ── identities ──────────────────────────────────────────────────────────────────────────────────────────────────

    protected sealed record StaffLogin(Guid StaffAccountId, string Mobile, string Password);

    protected async Task<StaffLogin> SeedStaffAsync(params string[] roles)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto("Test Staff", MobileNumber.Create(mobile), null, roles, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        return new StaffLogin(staff.Id, mobile, password);
    }

    protected async Task<HttpClient> SignInStaffAsync(StaffLogin login, Guid? branchId = null)
    {
        var client = CreateClient();
        await ReauthenticateAsync(client, login);
        if (branchId is { } id) client.DefaultRequestHeaders.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, id.ToString());
        return client;
    }

    /// <summary>Staff sessions idle out after 30 minutes; jumping the shared fake clock past that makes a signed-in session look idle. Re-sign in right after.</summary>
    protected static async Task ReauthenticateAsync(HttpClient client, StaffLogin login)
    {
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = login.Mobile, password = login.Password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
    }

    protected async Task<(HttpClient Client, Guid CustomerId, string Mobile)> RegisterAndSignInCustomerAsync()
    {
        var client = CreateClient();
        var captchaResponse = await client.PostAsync("/api/captcha", null);
        captchaResponse.EnsureSuccessStatusCode();
        var captchaId = (await captchaResponse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("captchaId").GetString()!;

        var mobile = NewMobile();
        const string password = "correcthorsebattery";
        var register = await client.PostAsJsonAsync("/api/customers", new { fullName = "Test Customer", mobileNumber = mobile, password, captchaId, captchaAnswer = "TEST1" }, Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var customerId = (await register.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("customerId").GetGuid();

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        return (client, customerId, mobile);
    }

    protected async Task<Guid> SeedCatAsync(Guid ownerId, string name = "Test Cat")
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();

        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId, name, "Cat", CatSex.Female, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2), DateOfBirthPrecision.Exact, "Domestic Shorthair", "Tabby",
            DateOnly.FromDateTime(DateTime.UtcNow), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));

        await repository.SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);
        return cat.Id;
    }

    // ── a branch to board in ────────────────────────────────────────────────────────────────────────────────────────

    protected sealed record BoardingBranch(HttpClient Admin, StaffLogin AdminLogin, Guid BranchId, Guid TaxClassId);

    /// <summary>A fresh branch (Asia/Dhaka), a tax class, and an administrator assigned to the branch and sending <c>X-Branch-Id</c>.</summary>
    protected async Task<BoardingBranch> SeedBranchAsync()
    {
        var login = await SeedStaffAsync(StaffRoles.Administrator);
        var admin = await SignInStaffAsync(login);

        var code = NewCode();
        var createBranch = await admin.PostAsJsonAsync("/api/admin/branches", new { code, name = "Boarding Branch " + code, timeZoneId = "Asia/Dhaka" }, Json);
        Assert.True(createBranch.StatusCode == HttpStatusCode.Created, await createBranch.Content.ReadAsStringAsync());
        var branchId = (await createBranch.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("branchId").GetGuid();

        var assign = await admin.PutAsJsonAsync($"/api/admin/staff/{login.StaffAccountId}/branches", new { branchIds = new[] { branchId }, primaryBranchId = branchId }, Json);
        Assert.True(assign.StatusCode == HttpStatusCode.NoContent, await assign.Content.ReadAsStringAsync());
        admin.DefaultRequestHeaders.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, branchId.ToString());

        var taxClass = await admin.PostAsJsonAsync("/api/admin/tax-classes", new { code = NewCode(), name = "Standard " + NewCode() }, Json);
        Assert.True(taxClass.StatusCode == HttpStatusCode.Created, await taxClass.Content.ReadAsStringAsync());
        var taxClassId = (await taxClass.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("taxClassId").GetGuid();

        return new BoardingBranch(admin, login, branchId, taxClassId);
    }

    protected async Task<Guid> CreateUnitAsync(BoardingBranch branch, string type = "Standard", int capacity = 1, string? code = null, string? block = "A")
    {
        var response = await branch.Admin.PostAsJsonAsync("/api/staff/boarding/units",
            new { code = code ?? NewCode(), name = type + " unit", unitType = type, capacityCats = capacity, block, sortOrder = 1 }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("unitId").GetGuid();
    }

    /// <summary>A rate card effective now (the fake clock does not tick, so it is in force at once).</summary>
    protected async Task SetRateCardAsync(BoardingBranch branch, string type = "Standard", string nightly = "1000.00", string weekly = "6000.00", string monthly = "24000.00",
        string additionalCat = "0.00", DateTimeOffset? effectiveFrom = null)
    {
        var response = await branch.Admin.PostAsJsonAsync("/api/staff/boarding/rate-cards", new
        {
            unitType = type, nightlyRate = nightly, weeklyRate = weekly, monthlyRate = monthly, additionalCatNightlyRate = additionalCat,
            taxClassId = branch.TaxClassId, effectiveFrom = effectiveFrom ?? Now
        }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    // ── vaccination, enquiries, profiles ────────────────────────────────────────────────────────────────────────────

    private static readonly SemaphoreSlim VaccineLock = new(1, 1);

    /// <summary>The Boarding fixture's OWN core vaccines (code prefix BRD, 12 months) -- created when there are none. The Boarding host's gate looks at these only
    /// (<see cref="BoardingTestVaccineScope"/>), so no other area's vaccine can leak into a test.</summary>
    protected async Task<IReadOnlyList<(Guid Id, string Code, string Name)>> CoreVaccinesAsync()
    {
        await VaccineLock.WaitAsync();
        try
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<VaccineAggregate>>();
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();

            var existing = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(
                    db.Set<VaccineAggregate>().Where(v => v.IsCore && v.IsActive && v.Code.StartsWith(BoardingTestVaccineScope.CodePrefix))));
            if (existing.Count == 0)
            {
                var vaccine = new VaccineAggregate(Guid.NewGuid());
                vaccine.Register(new VaccineRegistrationDto(BoardingTestVaccineScope.CodePrefix + NewCode(), "Boarding test core vaccine", true, 12, null, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
                await repository.SaveAsync(vaccine, EventPublishOption.InMemoryPublishOnly);
                existing = [vaccine];
            }

            return existing.Select(v => (v.Id, v.Code, v.Name)).ToList();
        }
        finally
        {
            VaccineLock.Release();
        }
    }

    /// <summary>Records every active core vaccine on a cat; <paramref name="administeredMonthsAgo"/> decides when they fall due (a vaccine's validity is its own).</summary>
    protected async Task VaccinateAsync(Guid catId, int administeredMonthsAgo = 0)
    {
        var vaccines = await CoreVaccinesAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();
        var vaccineRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<VaccineAggregate>>();
        var cat = (await repository.GetByIdAsync(catId))!;
        var today = DateOnly.FromDateTime(Now.UtcDateTime); // the fake clock's day: a class's clock may be weeks ahead of the real one
        var given = today.AddMonths(-administeredMonthsAgo);

        foreach (var vaccine in vaccines)
        {
            var validity = (await vaccineRepository.GetByIdAsync(vaccine.Id))!.ValidityMonths;
            try
            {
                cat.RecordVaccination(new CatVaccinationDto(
                    Guid.NewGuid(), vaccine.Id, vaccine.Code, vaccine.Name, true, validity, "LOT1", given.AddYears(2), given, Guid.NewGuid(), null, today,
                    new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            }
            catch (CatEcommerce.Domain.Exceptions.DomainRuleViolationException ex) when (ex.ErrorCode == CatEcommerce.Shared.Common.Constants.BusinessViolationCodes.DuplicateVaccinationOnDate)
            {
                // already recorded today (a second pass after another class added a core vaccine)
            }
        }

        await repository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);
    }

    /// <summary>A cat vaccinated TODAY against every active core vaccine (some other test class may have created one with a validity of a month), owned by <paramref name="ownerId"/>.</summary>
    protected async Task<Guid> SeedVaccinatedCatAsync(Guid ownerId, string name = "Test Cat")
    {
        var catId = await SeedCatAsync(ownerId, name);
        await VaccinateAsync(catId);
        return catId;
    }

    /// <summary>The branch-local calendar date of the fake clock (Asia/Dhaka, UTC+6).</summary>
    protected DateOnly LocalToday => DateOnly.FromDateTime(Now.ToOffset(TimeSpan.FromHours(6)).DateTime);

    protected static object ProfileBody(string foodSource = "OwnFood", bool vetAuthorised = true, string limit = "5000.00") => new
    {
        feedings = new[] { new { time = "08:00", portion = "1/3 cup dry", foodSource, foodDescription = "Own dry food" }, new { time = "18:00", portion = "1/3 cup dry", foodSource, foodDescription = "Own dry food" } },
        medications = new[] { new { name = "Metacam", dose = "0.5 ml", route = "oral", times = new[] { "09:00" } } },
        litterType = "Clumping",
        vetVisitAuthorised = vetAuthorised,
        vetSpendingLimit = limit,
        emergencyContact = new { name = "Aunt May", phone = "01711111111", relationship = "Aunt" },
        notes = "Shy",
    };

    protected async Task<HttpResponseMessage> EnquireAsync(HttpClient customer, Guid branchId, DateOnly arrival, DateOnly departure, string type, params Guid[] catIds) =>
        await customer.PostAsJsonAsync("/api/customers/me/boarding/enquiries",
            new { branchId, arrivalOn = arrival.ToString("yyyy-MM-dd"), departureOn = departure.ToString("yyyy-MM-dd"), unitType = type, catIds }, Json);

    protected async Task<Guid> EnquireOkAsync(HttpClient customer, Guid branchId, DateOnly arrival, DateOnly departure, string type, params Guid[] catIds)
    {
        var response = await EnquireAsync(customer, branchId, arrival, departure, type, catIds);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await ReadAsync(response)).GetProperty("stayId").GetGuid();
    }

    protected async Task SetProfileAsync(HttpClient customer, Guid stayId, Guid catId)
    {
        var response = await customer.PutAsJsonAsync($"/api/customers/me/boarding/stays/{stayId}/cats/{catId}/care-profile", ProfileBody(), Json);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    protected static Task<HttpResponseMessage> ReserveAsync(HttpClient customer, Guid stayId) =>
        customer.PostAsync($"/api/customers/me/boarding/stays/{stayId}/reserve", null);

    /// <summary>Enquire, give every cat a profile, and reserve. Returns the stay id.</summary>
    protected async Task<Guid> ReserveOkAsync(HttpClient customer, Guid branchId, DateOnly arrival, DateOnly departure, string type, params Guid[] catIds)
    {
        var stayId = await EnquireOkAsync(customer, branchId, arrival, departure, type, catIds);
        foreach (var catId in catIds) await SetProfileAsync(customer, stayId, catId);
        var reserve = await ReserveAsync(customer, stayId);
        Assert.True(reserve.StatusCode == HttpStatusCode.OK, await reserve.Content.ReadAsStringAsync());
        return stayId;
    }

    /// <summary>Staff record the deposit in Billing as an earmarked payment (merged Billing's mechanism). Returns the payment id.</summary>
    protected async Task<Guid> RecordDepositAsync(BoardingBranch branch, Guid customerId, Guid stayId, string amount, string method = "CashInStore")
    {
        var response = await branch.Admin.PostAsJsonAsync("/api/staff/billing/payments", new
        {
            customerId, method, amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), receivedOn = LocalToday.ToString("yyyy-MM-dd"),
            collector = new { kind = "Staff", staffAccountId = branch.AdminLogin.StaffAccountId },
            externalReference = method == "BankTransfer" ? "TRX" + NewCode() : null,
            earmark = new { kind = "BoardingStay", sourceId = stayId, label = "Boarding deposit" },
        }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await ReadAsync(response)).GetProperty("paymentId").GetGuid();
    }

    protected async Task<List<string>> NotificationKeysAsync(Guid recipientId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.Set<CatEcommerce.Domain.Aggregates.Notification.NotificationRequestAggregate>().Where(n => n.RecipientId == recipientId).Select(n => n.TemplateKey));
    }

    protected async Task RunBoardingWorkCycleAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatEcommerce.Api.Background.BoardingWorkCycle>().RunAsync();
    }

    /// <summary>A customer session idles out on the fake clock too; sign in again after a big jump.</summary>
    protected static async Task ReauthenticateCustomerAsync(HttpClient client, string mobile)
    {
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = "correcthorsebattery" }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
    }

    // ── an in-house stay ────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly string[] Days = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    /// <summary>A Caretaker assigned to the branch and rostered 00:00-23:45 every day (boarding runs outside clinic hours).</summary>
    protected async Task<StaffLogin> SeedCaretakerAsync(BoardingBranch branch)
    {
        var caretaker = await SeedStaffAsync(StaffRoles.Caretaker);
        var assign = await branch.Admin.PutAsJsonAsync($"/api/admin/staff/{caretaker.StaffAccountId}/branches", new { branchIds = new[] { branch.BranchId }, primaryBranchId = branch.BranchId }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);
        var slots = Days.Select(d => new { dayOfWeek = d, starts = "00:00", ends = "23:45" }).ToArray();
        var pattern = await branch.Admin.PutAsJsonAsync($"/api/admin/branches/{branch.BranchId}/rosters/{caretaker.StaffAccountId}/pattern", new { slots }, Json);
        Assert.True(pattern.StatusCode == HttpStatusCode.NoContent, await pattern.Content.ReadAsStringAsync());
        return caretaker;
    }

    protected static object TypedSignature(string name = "Test Owner") => new { signerName = name, method = "Typed", confirmedStatement = true };

    protected static object CheckInBody(Guid[] catIds, bool complete = true) => new
    {
        cats = catIds.Select(id => new
        {
            catId = id, identityConfirmed = true, vaccinationProofVerified = true, weightKg = complete ? 4.2m : (decimal?)null,
            arrivalHealthObservation = "Bright and alert", noBelongings = true, belongings = Array.Empty<object>(),
            signature = complete ? TypedSignature() : null,
        }).ToArray()
    };

    protected sealed record InHouse(BoardingBranch Branch, StaffLogin Caretaker, HttpClient Customer, string CustomerMobile, Guid CustomerId, Guid StayId, Guid[] CatIds, Guid UnitId, DateOnly Arrival);

    /// <summary>Reserved, paid, confirmed and checked in: arrives tomorrow, the fake clock moves to 14:30 that day, a caretaker is rostered.</summary>
    protected async Task<InHouse> CheckedInStayAsync(int nights = 5, int cats = 1, string unitType = "Standard", int capacity = 1, string deposit = "5000.00")
    {
        var branch = await SeedBranchAsync();
        var unit = await CreateUnitAsync(branch, unitType, capacity);
        await SetRateCardAsync(branch, unitType, nightly: "1000.00");
        var caretaker = await SeedCaretakerAsync(branch);
        var (customer, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        var catIds = new List<Guid>();
        for (var i = 0; i < cats; i++) catIds.Add(await SeedVaccinatedCatAsync(customerId, "Cat" + i));

        var arrival = LocalToday.AddDays(1);
        var stayId = await ReserveOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(nights), unitType, catIds.ToArray());
        await RecordDepositAsync(branch, customerId, stayId, deposit);
        var confirm = await branch.Admin.PostAsync($"/api/staff/boarding/stays/{stayId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var arrivalAt = new DateTimeOffset(arrival.Year, arrival.Month, arrival.Day, 14, 30, 0, TimeSpan.FromHours(6));
        if (arrivalAt > Now) Factory.Clock.SetUtcNow(arrivalAt.ToUniversalTime());
        await ReauthenticateAsync(branch.Admin, branch.AdminLogin);
        await ReauthenticateCustomerAsync(customer, mobile);

        var checkIn = await branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stayId}/check-in", CheckInBody(catIds.ToArray()), Json);
        Assert.True(checkIn.StatusCode == HttpStatusCode.OK, await checkIn.Content.ReadAsStringAsync());
        return new InHouse(branch, caretaker, customer, mobile, customerId, stayId, catIds.ToArray(), unit, arrival);
    }

    protected static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(text) ? default : JsonSerializer.Deserialize<JsonElement>(text, Json);
    }
}

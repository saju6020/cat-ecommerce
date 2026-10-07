using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Vaccination;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Api.Background;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>HO-NTF-001 §16 -- §16.1 rules apply (fresh ids per test, never assert emptiness, never
/// TRUNCATE). Helpers mint fresh staff, customers and cats per test, and run the notification
/// worker's passes directly (the worker loop itself is off in Testing).</summary>
public abstract class NotificationsIntegrationTestBase(NotificationsApiFactory factory)
    : CatEcommerceIntegrationTestBase<NotificationsApiFactory>(factory)
{
    protected override HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        HandleCookies = true
    });

    protected static string NewMobile() => "01" + Random.Shared.NextInt64(100_000_000, 999_999_999);
    protected static string NewEmail() => $"t-{Guid.NewGuid():N}@example.test";

    /// <summary>
    /// HO-NTF-001 gate fix (2026-09-27, TEST -- SECURITY's own flakiness note on
    /// SearchByRecipient/QuietHours/UnsubscribeBeforeDelivery). A fixed HISTORICAL literal (e.g.
    /// "2026-06-16T06:00:00Z") is safely inside Asia/Dhaka daytime (UTC+6, no DST -- 02:00-15:00 UTC
    /// is 08:00-21:00 local), but because it never changes, every repeated run of the suite against
    /// this shared, persistent, never-truncated database (§16.1) writes MORE rows into the exact
    /// same instant, and a query's own <c>from</c>/<c>to</c> range around that literal stops
    /// isolating "this run's data" at all -- it isolates nothing, because every previous run's rows
    /// are in that same window too. A query with no other narrowing filter (e.g. by <c>templateKey</c>
    /// alone, paged) can then push a fresh test's own row off the first page.
    ///
    /// Returns a value anchored to the REAL current instant (so every test run gets its own,
    /// never-repeated few-minute window) but shifted, if necessary, into the same safe UTC daytime
    /// band a fixed literal would have used -- still never inside the default 21:00-08:00 Dhaka
    /// quiet-hours window, so callers that are not specifically testing quiet hours still never
    /// fall into it.
    /// </summary>
    protected static DateTimeOffset SafeDaytimeInstant()
    {
        var now = DateTimeOffset.UtcNow;
        return now.Hour is >= 2 and < 15 ? now : now.AddHours(12);
    }

    protected static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [];
        var header = setCookies.First(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal));
        var rawValue = header[(cookieName.Length + 1)..].Split(';')[0];
        return Uri.UnescapeDataString(rawValue);
    }

    protected async Task<(Guid StaffAccountId, string Mobile, string Password)> SeedStaffAsync(params string[] roles)
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

        return (staff.Id, mobile, password);
    }

    protected async Task<HttpClient> SignInStaffAsync(string mobile, string password)
    {
        var client = CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return client;
    }

    /// <summary>Registers and signs in a fresh customer, optionally with an email address too
    /// (added via the profile update endpoint) so preferred-channel/email-delivery tests have one.</summary>
    protected async Task<(HttpClient Client, Guid CustomerId, string Mobile)> RegisterAndSignInCustomerAsync(string? email = null)
    {
        var client = CreateClient();
        var captchaResponse = await client.PostAsync("/api/captcha", null);
        captchaResponse.EnsureSuccessStatusCode();
        var captchaBody = await captchaResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var captchaId = captchaBody.GetProperty("captchaId").GetString()!;

        var mobile = NewMobile();
        const string password = "correcthorsebattery";

        var register = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Test Customer", mobileNumber = mobile, password, captchaId, captchaAnswer = "TEST1"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registerBody = await register.Content.ReadFromJsonAsync<JsonElement>(Json);
        var customerId = registerBody.GetProperty("customerId").GetGuid();

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        if (email is not null)
        {
            var update = await client.PutAsJsonAsync("/api/customers/me", new
            {
                fullName = "Test Customer", emailAddress = email, additionalPhoneNumbers = Array.Empty<string>(), currentPassword = password
            }, Json);
            Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        }

        return (client, customerId, mobile);
    }

    /// <summary>"Today" exactly as <c>CareReminderSweep</c> computes it: the main branch's local date
    /// (Asia/Dhaka when unset), not the UTC date. The two differ for several hours a day (18:00-24:00
    /// UTC for UTC+6), so a due date seeded from the UTC date lands a day off the sweep's window.</summary>
    protected async Task<DateOnly> BusinessTodayAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var branchRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<BranchAggregate>>();
        var mainBranch = await branchRepository.GetByIdAsync(AdministrationSeed.MainBranchId);
        return BranchCalendar.LocalDateOf(DateTimeOffset.UtcNow, mainBranch?.TimeZoneId ?? "Asia/Dhaka");
    }

    /// <summary>Registers a fresh, active vaccine (so the reminder sweep's active-vaccine filter
    /// finds it) and records a vaccination for <paramref name="catId"/> whose NextDueOn lands
    /// exactly <paramref name="dueInDays"/> days from the business-local today (<see cref="BusinessTodayAsync"/>).</summary>
    protected async Task<Guid> SeedVaccinationDueInAsync(Guid catId, int dueInDays)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var vaccineRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<VaccineAggregate>>();
        var catRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();

        var vaccine = new VaccineAggregate(Guid.NewGuid());
        vaccine.Register(new VaccineRegistrationDto($"V-{Guid.NewGuid():N}"[..12], "Rabies", true, 12, null, TestUserContext(), Guid.NewGuid()));
        await vaccineRepository.SaveAsync(vaccine, EventPublishOption.InMemoryPublishOnly);

        var administeredOn = (await BusinessTodayAsync()).AddDays(dueInDays).AddMonths(-12);

        var cat = await catRepository.GetByIdAsync(catId);
        cat.RecordVaccination(new CatVaccinationDto(
            Guid.NewGuid(), vaccine.Id, vaccine.Code, vaccine.Name, vaccine.IsCore, vaccine.ValidityMonths,
            "BATCH-1", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), administeredOn, Guid.NewGuid(), null,
            DateOnly.FromDateTime(DateTime.UtcNow), TestUserContext(), Guid.NewGuid()));
        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);

        return vaccine.Id;
    }

    /// <summary>I-F6's setup half: records a SECOND vaccination for the SAME, already-registered
    /// <paramref name="vaccineId"/> (the "renewed" case -- <c>NextDueOn</c> moves, but it is still
    /// the same vaccine the earlier request's <c>SubjectRef</c> points at). Unlike
    /// <see cref="SeedVaccinationDueInAsync"/>, this never registers a new vaccine.</summary>
    protected async Task SeedAdditionalVaccinationAsync(Guid catId, Guid vaccineId, int dueInDays)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var vaccineRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<VaccineAggregate>>();
        var catRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();

        var vaccine = await vaccineRepository.GetByIdAsync(vaccineId);
        var administeredOn = (await BusinessTodayAsync()).AddDays(dueInDays).AddMonths(-12);

        var cat = await catRepository.GetByIdAsync(catId);
        cat.RecordVaccination(new CatVaccinationDto(
            Guid.NewGuid(), vaccine.Id, vaccine.Code, vaccine.Name, vaccine.IsCore, vaccine.ValidityMonths,
            "BATCH-2", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), administeredOn, Guid.NewGuid(), null,
            DateOnly.FromDateTime(DateTime.UtcNow), TestUserContext(), Guid.NewGuid()));
        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);
    }

    /// <summary>Records a Combined parasite treatment for <paramref name="catId"/> whose NextDueOn
    /// lands exactly <paramref name="dueInDays"/> days from the business-local today (<see cref="BusinessTodayAsync"/>).</summary>
    protected async Task SeedDewormingDueInAsync(Guid catId, int dueInDays, int intervalDays = 90)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var catRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();

        var administeredOn = (await BusinessTodayAsync()).AddDays(dueInDays - intervalDays);

        var cat = await catRepository.GetByIdAsync(catId);
        cat.RecordParasiteTreatment(new CatParasiteTreatmentDto(
            Guid.NewGuid(), ParasiteTreatmentType.Combined, "Broad-spectrum", "BATCH-1", administeredOn, Guid.NewGuid(), intervalDays, null,
            DateOnly.FromDateTime(DateTime.UtcNow), TestUserContext(), Guid.NewGuid()));
        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);
    }

    protected async Task MarkCatDeceasedAsync(Guid catId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var catRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();
        var cat = await catRepository.GetByIdAsync(catId);
        cat.MarkDeceased(new CatDeceasedDto(DateOnly.FromDateTime(DateTime.UtcNow), null, DateOnly.FromDateTime(DateTime.UtcNow), TestUserContext(), Guid.NewGuid()));
        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);
    }

    /// <summary>I-F5's setup half: transfers a cat to a different owner, exactly the change the
    /// send-time guard (§8.7) must catch between enqueue and delivery.</summary>
    protected async Task TransferCatOwnershipAsync(Guid catId, Guid newOwnerId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var catRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();
        var cat = await catRepository.GetByIdAsync(catId);
        cat.TransferOwnership(new CatOwnershipTransferDto(newOwnerId, "Sold to a new owner", TestUserContext(), Guid.NewGuid()));
        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);
    }

    protected async Task<Guid> SeedCatAsync(Guid ownerId, string name = "Test Cat")
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();

        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId, name, "Cat", CatSex.Female, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2),
            DateOfBirthPrecision.Exact, "Domestic Shorthair", "Tabby", DateOnly.FromDateTime(DateTime.UtcNow), TestUserContext(), Guid.NewGuid()));

        await repository.SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);
        return cat.Id;
    }

    private static UserContext TestUserContext() => new(Guid.NewGuid(), Guid.NewGuid());

    /// <summary>Runs one delivery pass (expand pending requests, deliver due notifications, redact
    /// due secrets) in a fresh scope -- the same shape <see cref="NotificationWorkCycle"/> itself
    /// uses per item, just driven directly instead of by the (disabled) hosted worker.</summary>
    protected async Task RunDeliveryPassAsync(WebApplicationFactory<Program>? factoryOverride = null)
    {
        var services = (factoryOverride ?? Factory).Services;
        await using var scope = services.CreateAsyncScope();
        var workCycle = scope.ServiceProvider.GetRequiredService<NotificationWorkCycle>();
        await workCycle.RunDeliveryPassAsync();
    }

    protected async Task RunReminderSweepAsync(WebApplicationFactory<Program>? factoryOverride = null)
    {
        var services = (factoryOverride ?? Factory).Services;
        await using var scope = services.CreateAsyncScope();
        var workCycle = scope.ServiceProvider.GetRequiredService<NotificationWorkCycle>();
        await workCycle.RunReminderSweepAsync();
    }

    /// <summary>Dispatches a <c>RequestNotificationCommand</c> directly, in a fresh scope -- there
    /// is no HTTP surface for it (§11.1 C3: "sent by timed producers ... and tests").</summary>
    protected async Task<CatEcommerce.Application.Commands.Notification.RequestNotificationCommand> RequestNotificationAsync(
        WebApplicationFactory<Program>? factoryOverride,
        string templateKey, NotificationRecipientKind recipientKind, Guid recipientId,
        IReadOnlyDictionary<string, string> variables, Guid? branchId = null, string? deduplicationKey = null,
        NotificationSubjectKind? subjectKind = null, Guid? subjectId = null, Guid? subjectRef = null, DateOnly? subjectDueOn = null, DateTimeOffset? notAfter = null)
    {
        var command = new CatEcommerce.Application.Commands.Notification.RequestNotificationCommand
        {
            RequestId = Guid.NewGuid(), TemplateKey = templateKey, RecipientKind = recipientKind.DisplayName, RecipientId = recipientId,
            Variables = variables, BranchId = branchId, DeduplicationKey = deduplicationKey,
            SubjectKind = subjectKind?.DisplayName, SubjectId = subjectId, SubjectRef = subjectRef, SubjectDueOn = subjectDueOn, NotAfter = notAfter,
            UserContext = TestUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var services = (factoryOverride ?? Factory).Services;
        await using var scope = services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.IInMemoryDispatcher>();
        var response = await dispatcher.SendAsync(command);
        Assert.True(response.IsSuccess, "RequestNotificationCommand failed: " + JsonSerializer.Serialize(response.ValidationResult));

        return command;
    }

    /// <summary>Dispatches only <c>ExpandNotificationRequestCommand</c> (not the whole delivery
    /// pass) for a request that is already Pending -- lets a test observe the request expanded into
    /// a still-Queued notification, without the same pass immediately going on to deliver it too.</summary>
    protected async Task ExpandOnlyAsync(WebApplicationFactory<Program>? factoryOverride, Guid requestId)
    {
        var services = (factoryOverride ?? Factory).Services;
        await using var scope = services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.IInMemoryDispatcher>();
        var response = await dispatcher.SendAsync(new CatEcommerce.Application.Commands.Notification.ExpandNotificationRequestCommand
        {
            RequestId = requestId, UserContext = TestUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });
        Assert.True(response.IsSuccess, "ExpandNotificationRequestCommand failed: " + JsonSerializer.Serialize(response.ValidationResult));
    }
}

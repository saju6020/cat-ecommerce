using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Api.Branches;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-EMR-001 §16.1 -- every test creates its own branch, rooms, rostered vet and nurse, customer, cat and
/// checked-in appointment through HO-ADM-001 / HO-APT-001 endpoints, so no test depends on another's data
/// and none disables a trigger (rule 6). A walk-in is the quickest legitimate way to a
/// <c>CheckedIn</c> appointment (HO-APT-001 §8.3), and needs no controllable clock.
/// </summary>
public abstract class ConsultationIntegrationTestBase(ConsultationApiFactory factory) : AppointmentsIntegrationTestBase(factory), IClassFixture<ConsultationApiFactory>, IDisposable
{
    protected new ConsultationApiFactory Factory => factory;

    /// <summary>Everything a clinical test needs, as separate signed-in clients (each has its own cookie jar).</summary>
    protected sealed record ClinicalWorld(
        WebApplicationFactory<Program> Api,
        FakeTimeProvider Clock,
        ClinicTestContext Context,
        HttpClient Admin,
        HttpClient Vet,
        HttpClient Nurse,
        HttpClient Reception,
        HttpClient Customer,
        Guid CustomerId,
        string CustomerMobile,
        Guid CatId,
        Guid AppointmentId,
        Guid VetStaffId,
        Guid NurseStaffId,
        Guid ReceptionStaffId,
        Guid AdminStaffId)
    {
        public Guid BranchId => Context.BranchId;
        public Guid ServiceId => Context.ServiceId;
    }

    protected async Task<HttpClient> SignInStaffOnAsync(WebApplicationFactory<Program> api, string mobile, string password, Guid branchId)
    {
        var client = CreateClientOn(api);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        client.DefaultRequestHeaders.Add(HttpContextActiveBranchAccessor.BranchHeaderName, branchId.ToString());
        return client;
    }

    /// <summary>A staff member of <paramref name="roles"/>, assigned to the branch, signed in on <paramref name="api"/>.</summary>
    protected async Task<(HttpClient Client, Guid StaffId, string Mobile, string Password)> AddStaffAsync(
        WebApplicationFactory<Program> api, HttpClient admin, Guid branchId, params string[] roles)
    {
        var staff = await SeedStaffAsync(roles);
        var assign = await admin.PutAsJsonAsync($"/api/admin/staff/{staff.StaffAccountId}/branches", new { branchIds = new[] { branchId }, primaryBranchId = branchId }, Json);
        Assert.True(assign.StatusCode == HttpStatusCode.NoContent, await assign.Content.ReadAsStringAsync());
        var client = await SignInStaffOnAsync(api, staff.Mobile, staff.Password, branchId);
        return (client, staff.StaffAccountId, staff.Mobile, staff.Password);
    }

    private readonly List<IDisposable> _hosts = [];

    /// <summary>Releases the per-test hosts <see cref="SeedClinicalWorldAsync"/> creates.</summary>
    public void Dispose()
    {
        foreach (var host in _hosts) host.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Branch + vet + nurse + reception + customer + cat + a checked-in walk-in appointment, on a host whose
    /// clock is a <see cref="FakeTimeProvider"/> starting at <paramref name="start"/> (default: now). A controlled
    /// clock is what lets a service price be effective "now" with no waiting (HO-APT-001 §16.1), and lets a test
    /// advance time for signed-URL expiry and reminder send times. The host shares this class's database and
    /// file-storage root and is disposed with the test.
    /// </summary>
    protected async Task<ClinicalWorld> SeedClinicalWorldAsync(DateTimeOffset? start = null, Action<IServiceCollection>? configureServices = null)
    {
        var clock = new FakeTimeProvider(start ?? DateTimeOffset.UtcNow);
        var api = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(clock);
            RealClockCookieExpiry.Apply(services); // the cookie jar uses the real clock
            configureServices?.Invoke(services);
        }));
        _hosts.Add(api);

        var (admin, context, adminAccount) = await SeedBranchWithAdminAssignedAsync(api, clock.GetUtcNow());
        var vet = await SignInStaffOnAsync(api, context.VetMobile, context.VetPassword, context.BranchId);
        var nurse = await AddStaffAsync(api, admin, context.BranchId, StaffRoles.Nurse);
        var reception = await AddStaffAsync(api, admin, context.BranchId, StaffRoles.Reception);

        var (customer, customerId, mobile) = await RegisterAndSignInCustomerOnAsync(api);
        var catId = await SeedCatAsync(customerId, "Mitu");

        var appointmentId = await RegisterCheckedInWalkInAsync(admin, customerId, catId, context.ServiceId);

        return new ClinicalWorld(api, clock, context, admin, vet, nurse.Client, reception.Client, customer, customerId, mobile, catId, appointmentId,
            context.VetStaffAccountId, nurse.StaffId, reception.StaffId, adminAccount.StaffAccountId);
    }

    /// <summary>A further checked-in walk-in for an existing cat.</summary>
    protected static async Task<Guid> RegisterCheckedInWalkInAsync(HttpClient admin, Guid customerId, Guid catId, Guid serviceId)
    {
        var response = await admin.PostAsJsonAsync("/api/staff/walk-ins", new { customerId, catId, serviceId, triagePriority = "Routine" }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("appointmentId").GetGuid();
    }

    /// <summary>Opens a visit as the nurse and returns its id.</summary>
    protected static async Task<Guid> OpenVisitAsync(HttpClient staff, Guid appointmentId)
    {
        var response = await staff.PostAsJsonAsync("/api/staff/visits", new { appointmentId }, Json);
        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("visitId").GetGuid();
    }

    /// <summary>A fresh diagnosis code minted per test (§16.1 rule 2) -- unique key, so never reused.</summary>
    protected static string NewDiagnosisCode() => "T-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    protected static async Task<Guid> CreateDiagnosisCodeAsync(HttpClient vet, string? code = null)
    {
        var response = await vet.PostAsJsonAsync("/api/staff/diagnosis-codes", new { code = code ?? NewDiagnosisCode(), name = "Test diagnosis " + Guid.NewGuid().ToString("N")[..6] }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("diagnosisCodeId").GetGuid();
    }

    protected static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Json);

    // ── Direct database access (assertions and the raw-SQL immutability probes, I-C9) ────────────────

    protected static string ConnectionString =>
        TestDatabase.ConnectionString;

    protected static async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    protected static async Task<T?> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)Convert.ChangeType(result, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    /// <summary>Runs a statement that is expected to be refused by a trigger and returns its SQLSTATE.</summary>
    protected static async Task<string?> SqlStateOfAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        try
        {
            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.SqlState;
        }
    }

    protected async Task<int> CountAuditRowsAsync(AuditCategory category, string entityType, Guid? entityId = null, Guid? actor = null)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        return await db.Set<AuditEntryAggregate>().AsNoTracking()
            .CountAsync(a => a.Category == category && a.EntityType == entityType
                && (entityId == null || a.EntityId == entityId) && (actor == null || a.ActorUserId == actor));
    }

    /// <summary>"AuthorizationDenied" audit rows written for <paramref name="actor"/> on <paramref name="action"/>
    /// (HO-ACC-001: the row's <c>Reason</c> is the refused action's name).</summary>
    protected async Task<int> CountDenialsAsync(Guid actor, string action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        return await db.Set<AuditEntryAggregate>().AsNoTracking()
            .CountAsync(a => a.Action == "AuthorizationDenied" && a.ActorUserId == actor && a.Reason == action);
    }

    /// <summary>Every <c>ClinicalRecordAccess</c> audit row written for <paramref name="actor"/> (any kind, any record).</summary>
    protected async Task<int> CountAccessRowsAsync(Guid actor)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        return await db.Set<AuditEntryAggregate>().AsNoTracking()
            .CountAsync(a => a.Category == AuditCategory.ClinicalRecordAccess && a.ActorUserId == actor);
    }
}

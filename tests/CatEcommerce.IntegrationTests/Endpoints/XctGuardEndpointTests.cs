using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-XCT-001 section 16 A -- the hard-delete refusal, proven against the real database and the real host.</summary>
public sealed class XctGuardEndpointTests(XctApiFactory factory) : XctIntegrationTestBase(factory)
{
    // I-A1
    [Fact]
    public async Task I_A1_TheHostStartsWithScopeValidationOn_AndEveryXctRegistrationResolves()
    {
        // The host was built with ValidateScopes/ValidateOnBuild (Program.cs); creating a client forces the build.
        using var client = CreateClient();
        await using var scope = Factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        Assert.NotNull(provider.GetService<CatEcommerce.Infrastructure.Operations.DatabaseRoleGuard>());
        Assert.Contains(provider.GetServices<IPersonalDataEraser>().Select(e => e.ContextKey), key => key == "notification");
    }

    // I-A3 -- every Forbidden table, not a sample
    [Fact]
    public async Task I_A3_EveryForbiddenTable_HasARowDeleteTrigger_AndATruncateStatementTrigger()
    {
        await using var connection = await OpenDatabaseAsync();
        var failures = new List<string>();

        foreach (var entry in RecordClassification.All.Where(e => e.HardDeletePolicy == HardDeletePolicy.Forbidden))
        {
            // tgtype bits: 1 = ROW, 2 = BEFORE, 8 = DELETE, 16 = UPDATE, 32 = TRUNCATE.
            await using var command = new NpgsqlCommand("""
                SELECT t.tgtype
                FROM pg_trigger t
                JOIN pg_class c ON c.oid = t.tgrelid
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = @schema AND c.relname = @table AND NOT t.tgisinternal AND t.tgenabled = 'O'
                """, connection);
            command.Parameters.AddWithValue("schema", entry.Schema);
            command.Parameters.AddWithValue("table", entry.Table);

            var hasRowDelete = false;
            var hasTruncate = false;
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var type = reader.GetInt16(0);
                    var isBefore = (type & 2) != 0;
                    if (isBefore && (type & 1) != 0 && (type & 8) != 0) hasRowDelete = true;
                    if (isBefore && (type & 1) == 0 && (type & 32) != 0) hasTruncate = true;
                }
            }

            if (!hasRowDelete) failures.Add($"{entry.Schema}.{entry.Table}: no BEFORE DELETE row trigger");
            if (!hasTruncate) failures.Add($"{entry.Schema}.{entry.Table}: no BEFORE TRUNCATE statement trigger");
        }

        Assert.True(failures.Count == 0, string.Join("; ", failures));
    }

    [Fact]
    public async Task I_A3_TheRetentionFunctionExists_AndTheAuditTableKeepsOnlyItsOwnAccTriggers()
    {
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*)::int FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'retention' AND p.proname = 'refuse_hard_delete'"));
        Assert.Equal(0, await ScalarAsync<int>("""
            SELECT COUNT(*)::int FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'audit' AND c.relname = 'audit_entry' AND t.tgname IN ('trg_refuse_hard_delete', 'trg_refuse_truncate')
            """));
    }

    // I-A4
    [Fact]
    public async Task I_A4_ARawDelete_AndATruncate_AreRefusedByTheDatabase()
    {
        var (_, customerId, _, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId);

        await using var connection = await OpenDatabaseAsync();

        await using (var delete = new NpgsqlCommand("DELETE FROM clinic.cat WHERE \"Id\" = @id", connection))
        {
            delete.Parameters.AddWithValue("id", catId);
            var refused = await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());
            Assert.Equal("P0001", refused.SqlState);
            Assert.Contains("Hard delete of clinic.cat is not permitted", refused.MessageText);
        }

        await using var transaction = await connection.BeginTransactionAsync();
        await using (var truncate = new NpgsqlCommand("TRUNCATE clinic.appointment CASCADE", connection, transaction))
        {
            var refused = await Assert.ThrowsAsync<PostgresException>(() => truncate.ExecuteNonQueryAsync());
            Assert.Equal("P0001", refused.SqlState);
            Assert.Contains("Hard delete of clinic.appointment is not permitted", refused.MessageText);
        }

        await transaction.RollbackAsync();
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*)::int FROM clinic.cat WHERE \"Id\" = @id", ("id", catId)));
    }

    // I-A5
    [Fact]
    public async Task I_A5_ACodePathThatRemovesACat_Answers409RecordIsRetained_AndTheRowStays()
    {
        var (_, customerId, _, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId);

        using var anonymous = CreateClient();
        var response = await anonymous.PostAsync($"/api/test-only/xct/remove-cat/{catId}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var error = body.GetProperty("errors")[0];
        Assert.Equal("RecordIsRetained", error.GetProperty("errorCode").GetString());
        Assert.Equal(OperationsMessages.RecordIsRetained, error.GetProperty("errorMessage").GetString());
        Assert.DoesNotContain("clinic", error.GetProperty("errorMessage").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*)::int FROM clinic.cat WHERE \"Id\" = @id", ("id", catId)));
    }

    // I-A6
    [Fact]
    public async Task I_A6_UpdateMedicalProfileRemovingAnAllergy_StillWorks_AggregateOnlyIsNotBroken()
    {
        var (staff, _) = await SignedInStaffAsync(StaffRoles.Vet);
        var (_, customerId, _, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId);

        var add = await staff.PutAsJsonAsync($"/api/cats/{catId}/medical-profile", new
        {
            allergies = new[] { new { allergen = "Chicken", reaction = "Itching", severity = "Mild", recordedOn = DateOnly.FromDateTime(DateTime.UtcNow) } },
            chronicConditions = Array.Empty<object>(), medications = Array.Empty<object>()
        }, Json);
        Assert.True(add.IsSuccessStatusCode, await add.Content.ReadAsStringAsync());
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*)::int FROM clinic.cat_allergy WHERE cat_id = @id", ("id", catId)));

        var remove = await staff.PutAsJsonAsync($"/api/cats/{catId}/medical-profile", new
        {
            allergies = Array.Empty<object>(), chronicConditions = Array.Empty<object>(), medications = Array.Empty<object>()
        }, Json);

        Assert.True(remove.IsSuccessStatusCode, await remove.Content.ReadAsStringAsync());
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*)::int FROM clinic.cat_allergy WHERE cat_id = @id", ("id", catId)));
    }
}

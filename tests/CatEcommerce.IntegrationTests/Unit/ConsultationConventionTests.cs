using System.Reflection;
using CatEcommerce.Api.Controllers;
using CatEcommerce.Api.Filters;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence.Migrations;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>HO-EMR-001 §15 -- the structural checks that need the Api and Infrastructure assemblies: U-X3 (the immutability migration
/// is written from the enumeration and covers every protected table) and U-X5 (every action that returns clinical data is
/// access-logged).</summary>
public sealed class ConsultationConventionTests
{
    // ── U-X5 ────────────────────────────────────────────────────────────────────────────────────

    private static readonly Type[] ClinicalControllers =
    [
        typeof(StaffVisitsController), typeof(StaffDiagnosticsController), typeof(StaffClinicalHistoryController),
        typeof(StaffConsentsController), typeof(CustomerVisitsController),
    ];

    /// <summary>The explicit list of GET actions in the clinical controllers that return <b>no</b> clinical record: a visit id and
    /// status reference, the server's own range table, a consent form's wording (a policy document) and the yes/no consent status.</summary>
    private static readonly HashSet<string> NonClinicalGets =
    [
        nameof(StaffVisitsController.GetByAppointment),
        nameof(StaffVisitsController.GetVitalSignRules),
        nameof(StaffConsentsController.GetConsentForm),
        nameof(StaffConsentsController.GetStatus),
    ];

    [Fact]
    public void U_X5_EveryGetThatReturnsClinicalData_CarriesClinicalRecordAccess()
    {
        var gets = ClinicalControllers
            .SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpGetAttribute>().Any())
            .ToList();

        Assert.NotEmpty(gets);

        var unlogged = gets
            .Where(m => !NonClinicalGets.Contains(m.Name) && m.GetCustomAttribute<ClinicalRecordAccessAttribute>() is null)
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        Assert.Empty(unlogged);
    }

    [Fact]
    public void U_X5_TheExceptionListIsReal_EveryNamedActionExistsAndIsNotLogged()
    {
        foreach (var name in NonClinicalGets)
        {
            var method = ClinicalControllers.SelectMany(c => c.GetMethods()).FirstOrDefault(m => m.Name == name && m.GetCustomAttributes<HttpGetAttribute>().Any());
            Assert.NotNull(method);
        }
    }

    [Fact]
    public void ClinicalRecordAccess_RejectsAnUnknownKind_RatherThanLoggingNothing()
    {
        Assert.Throws<ArgumentException>(() => new ClinicalRecordAccessAttribute("NotAKind"));
        Assert.Equal(ClinicalRecordKind.Visit, new ClinicalRecordAccessAttribute(nameof(ClinicalRecordKind.Visit)).Kind);
    }

    // ── U-X3 ────────────────────────────────────────────────────────────────────────────────────

    private static string SqlOf<TMigration>() where TMigration : Migration, new()
    {
        var migration = new TMigration();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(TMigration).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        return string.Join("\n", builder.Operations.OfType<SqlOperation>().Select(o => o.Sql));
    }

    [Fact]
    public void U_X3_TheImmutabilityTriggers_UseTheEnumerationValue_NotALiteral()
    {
        var sql = SqlOf<AddClinicalRecordImmutability>();

        Assert.Contains($"OLD.status = {VisitStatus.Finalised.Value}", sql);
        Assert.Contains($"parent_status = {VisitStatus.Finalised.Value}", sql);
        Assert.Contains("ERRCODE = 'CR001'", sql);
    }

    [Fact]
    public void U_X3_TheHardeningMigration_UsesTheEnumerationValues_AndGuardsTheSoftDeleteFlag()
    {
        var sql = SqlOf<HardenClinicalAndBillingTriggers>();

        Assert.Contains($"parent_status = {VisitStatus.Finalised.Value}", sql);
        Assert.Contains($"OLD.status = {ConsentStatus.Withdrawn.Value}", sql);
        Assert.Contains("NEW.visit_id IS DISTINCT FROM OLD.visit_id", sql);
        Assert.Contains("BEFORE UPDATE ON clinic.diagnostic_order", sql);
        Assert.Contains("billing.fn_no_truncate", sql);
        // visit, diagnostic_order and consent_record each compare the flag
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(sql, "NEW\\.\"IsMarkedToDelete\" IS DISTINCT FROM OLD\\.\"IsMarkedToDelete\"").Count);
    }

    [Fact]
    public void U_X3_EveryProtectedTableHasItsTriggers_AcrossTheFourMigrations()
    {
        var sql = SqlOf<AddClinicalRecordImmutability>() + SqlOf<AddDiagnostics>() + SqlOf<AddConsents>() + SqlOf<AddVisitTreatments>();

        // The visit and its owned children, addenda, the diagnostic tables and consents -- each is frozen, append-only or no-delete,
        // and none can be truncated.
        string[] tables =
        [
            "clinic.visit", "clinic.visit_problem", "clinic.visit_vitals", "clinic.visit_photo", "clinic.visit_treatment", "clinic.visit_addendum",
            "clinic.diagnostic_order", "clinic.diagnostic_requested_analyte", "clinic.diagnostic_result_set",
            "clinic.diagnostic_result_value", "clinic.diagnostic_attachment", "clinic.consent_record",
        ];

        foreach (var table in tables)
        {
            Assert.Matches($@"BEFORE TRUNCATE ON {System.Text.RegularExpressions.Regex.Escape(table)}\b", sql);
            Assert.Matches($@"BEFORE (INSERT OR )?(UPDATE OR DELETE|UPDATE|DELETE) ON {System.Text.RegularExpressions.Regex.Escape(table)}\b", sql);
        }

        Assert.Contains($"OLD.status = {ConsentStatus.Withdrawn.Value}", sql);
        Assert.Contains("BEFORE DELETE ON files.stored_file", sql);
    }
}

using System.Reflection;
using System.Text.RegularExpressions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Infrastructure.Reporting;
using CatEcommerce.Read.QueryHandlers.Reporting;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>
/// HO-RPT-001 section 15 -- the convention tests that make "Reporting writes nothing outside its schema" and "no report without a source" mechanical rather than a review habit: U-A2, U-A3, U-D4,
/// U-X3 and U-X5. Source-text and reflection scans: no host, no database.
/// </summary>
public sealed class ReportingConventionTests
{
    /// <summary>Reporting's own migrations. Each is read: nothing may touch a schema other than <c>report</c> (ADR-277), save the HO-NTF-001 amendment's four columns.</summary>
    private static readonly string[] ReportingMigrations =
    [
        "AddReportingSchema", "AddClinicReportViews", "AddStockReportViews", "AddFinancialReportViews", "AddGrowthReportViews", "AddReportSchedules", "AddNotificationAttachments", "AddReorderViewsAndCustomerMergeChain", "AddOperationsReportViews",
    ];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CatEcommerce.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The repository root (CatEcommerce.sln) was not found.");
    }

    // ── U-A2 (static half): the sources in the Read assembly are exactly the catalogue ─────────────────────
    [Fact]
    public void U_A2_EveryCatalogueKey_HasExactlyOneSource_AndEverySourceHasADefinition()
    {
        var sourceTypes = typeof(IReportSource).Assembly.GetTypes().Where(t => typeof(IReportSource).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false }).ToList();
        var keys = sourceTypes.Select(t => ((IReportSource)Activator.CreateInstance(t)!).Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Equal(ReportCatalogue.All.Select(d => d.Key).Order(), keys.Order());
    }

    // ── U-A3: no write path in report code (ADR-279) ──────────────────────────────────────────────────────
    private static readonly string[] ForbiddenTokens = ["IAggregateRootRepository<", "SaveChanges", "ExecuteUpdate", "ExecuteDelete", "ExecuteSql", "FromSql"];

    [Fact]
    public void U_A3_NoReportSource_Tile_OrReportingInfrastructure_ContainsAWritePath()
    {
        var root = RepositoryRoot();
        var files = new List<string>();
        files.AddRange(Directory.GetFiles(Path.Combine(root, "src", "Read", "CatEcommerce.Read", "QueryHandlers", "Reporting"), "*.cs", SearchOption.AllDirectories));
        files.AddRange(Directory.GetFiles(Path.Combine(root, "src", "Infrastructure", "CatEcommerce.Infrastructure", "Reporting"), "*.cs", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f) != "EfScheduledReportRunStore.cs")); // the one write port: it saves Reporting's OWN run row (and the requests enlisted with it), never a report's data

        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var token in ForbiddenTokens)
                Assert.False(text.Contains(token, StringComparison.Ordinal), $"{Path.GetFileName(file)} contains '{token}' -- reports read; they never write (ADR-279).");
        }
    }

    // ── U-D4: no floating point in any view row, report model or ViewModel (C-6) ──────────────────────────
    [Fact]
    public void U_D4_NoViewRow_ReportModel_OrViewModel_HasADoubleOrFloatProperty()
    {
        var types = new List<Type>();
        types.AddRange(typeof(RvStaffRow).Assembly.GetTypes().Where(t => t.Namespace == typeof(RvStaffRow).Namespace));
        types.AddRange(typeof(ReportRow).Assembly.GetTypes().Where(t => t.Name.StartsWith("Report", StringComparison.Ordinal) || t.Name.StartsWith("Dashboard", StringComparison.Ordinal)));
        types.AddRange(typeof(IReportSource).Assembly.GetTypes().Where(t => t.Namespace == "CatEcommerce.Read.ViewModels" && (t.Name.StartsWith("Report", StringComparison.Ordinal) || t.Name.StartsWith("Dashboard", StringComparison.Ordinal))));

        Assert.NotEmpty(types);
        foreach (var type in types)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                Assert.False(propertyType == typeof(double) || propertyType == typeof(float), $"{type.Name}.{property.Name} is floating point (C-6).");
            }
        }
    }

    // ── U-X3: every keyless view entity maps ToView(…, "report") to a view a migration created ─────────────
    [Fact]
    public void U_X3_EveryKeylessReportViewEntity_IsMappedToAViewInTheReportSchema_AndHasAVersionedSqlFile()
    {
        var options = new DbContextOptionsBuilder<CatEcommerceDbContext>().UseNpgsql("Host=localhost;Database=unused").Options;
        using var context = new CatEcommerceDbContext(options);

        var viewEntities = context.Model.GetEntityTypes().Where(e => e.FindPrimaryKey() is null && e.GetViewSchema() == "report").ToList();
        Assert.NotEmpty(viewEntities);

        var sqlNames = ReportingViewSql.EmbeddedNames().Select(n => n.Split('.')[0]).ToHashSet(StringComparer.Ordinal);
        foreach (var entity in viewEntities)
        {
            Assert.NotNull(entity.GetViewName());
            Assert.Null(entity.GetTableName());
            Assert.Contains(entity.GetViewName()!, sqlNames);
        }

        // No keyless entity in the `report` schema is mapped as a table, and no Reporting view is mapped outside it.
        Assert.DoesNotContain(context.Model.GetEntityTypes(), e => e.FindPrimaryKey() is null && e.GetTableName() is not null && e.GetSchema() == "report");
    }

    [Fact]
    public void U_X3_TheSlotIndexPredicate_UsesTheEnumerationValues_NeverLiterals()
    {
        var root = RepositoryRoot();
        var configurations = Directory.GetFiles(Path.Combine(root, "src", "Infrastructure", "CatEcommerce.Infrastructure", "Persistence", "Configurations"), "ReportScheduleConfigurations.cs", SearchOption.AllDirectories);
        var text = File.ReadAllText(Assert.Single(configurations));
        Assert.Contains("ReportRunTrigger.Schedule.Value", text);
        Assert.Contains("ScheduledReportRunStatus.Generated.Value", text);
        Assert.Contains("ScheduledReportRunStatus.Purged.Value", text);
        Assert.Contains("ix_scheduled_report_run_slot", text);
        Assert.DoesNotMatch(new Regex(@"trigger\""?\s*=\s*\d"), text);
    }

    // ── U-X5: nothing outside schema `report` (ADR-277) ──────────────────────────────────────────────────
    [Fact]
    public void U_X5_EveryReportingMigration_TouchesOnlyTheReportSchema_ExceptTheNotificationAttachmentColumns()
    {
        var assembly = typeof(CatEcommerceDbContext).Assembly;
        var migrations = assembly.GetTypes().Where(t => typeof(Migration).IsAssignableFrom(t) && t.GetCustomAttribute<MigrationAttribute>() is not null
                                                       && ReportingMigrations.Any(name => t.Name == name)).ToList();
        Assert.Contains(migrations, m => m.Name == "AddReportingSchema");

        foreach (var type in migrations)
        {
            var migration = (Migration)Activator.CreateInstance(type)!;
            foreach (var operation in migration.UpOperations.Concat(migration.DownOperations))
            {
                switch (operation)
                {
                    case SqlOperation sql:
                        AssertSqlStaysInReport(type.Name, sql.Sql);
                        break;
                    case AddColumnOperation add when type.Name == "AddNotificationAttachments":
                        Assert.Equal("notify", add.Schema);
                        Assert.Contains(add.Name, new[] { "attachment_kind", "attachment_id" });
                        Assert.Contains(add.Table, new[] { "notification", "notification_request" });
                        break;
                    case DropColumnOperation drop when type.Name == "AddNotificationAttachments":
                        Assert.Equal("notify", drop.Schema);
                        break;
                    // AddReportSchedules creates Reporting's own two tables and their indexes -- in schema `report`, nowhere else.
                    case CreateTableOperation create when type.Name == "AddReportSchedules":
                        Assert.Equal("report", create.Schema);
                        Assert.Contains(create.Name, new[] { "report_schedule", "scheduled_report_run" });
                        break;
                    case DropTableOperation drop when type.Name == "AddReportSchedules":
                        Assert.Equal("report", drop.Schema);
                        break;
                    case CreateIndexOperation index when type.Name == "AddReportSchedules":
                        Assert.Equal("report", index.Schema);
                        Assert.Contains(index.Table, new[] { "report_schedule", "scheduled_report_run" });
                        break;
                    case DropIndexOperation dropIndex when type.Name == "AddReportSchedules":
                        Assert.Equal("report", dropIndex.Schema);
                        break;
                    case EnsureSchemaOperation ensure when type.Name == "AddReportSchedules":
                        Assert.Equal("report", ensure.Name);
                        break;
                    case CreateTableOperation or DropTableOperation or CreateIndexOperation or DropIndexOperation or AddColumnOperation or AlterTableOperation or EnsureSchemaOperation
                        or AddPrimaryKeyOperation or DropPrimaryKeyOperation or AlterColumnOperation or DropColumnOperation or RenameTableOperation:
                        Assert.Fail($"{type.Name}: {operation.GetType().Name} -- {Describe(operation)}");
                        break;
                }
            }
        }
    }

    private static string Describe(MigrationOperation operation) => operation switch
    {
        CreateTableOperation c => $"{c.Schema}.{c.Name}",
        CreateIndexOperation c => $"{c.Schema}.{c.Table}.{c.Name}",
        _ => operation.ToString() ?? "",
    };

    private static void AssertSqlStaysInReport(string migration, string sql)
    {
        // Every statement that creates, drops or alters a relation must target the `report` schema.
        foreach (Match match in Regex.Matches(sql, @"(?:CREATE\s+(?:OR\s+REPLACE\s+)?(?:UNIQUE\s+)?(?:VIEW|TABLE|INDEX|SCHEMA)|DROP\s+(?:VIEW|TABLE|INDEX|SCHEMA)|ALTER\s+(?:TABLE|VIEW))\s+(?:IF\s+(?:NOT\s+)?EXISTS\s+)?([\w\.""]+)", RegexOptions.IgnoreCase))
        {
            var target = match.Groups[1].Value.Trim('"').ToLowerInvariant();
            var isReportObject = target == "report" || target.StartsWith("report.", StringComparison.Ordinal) || target.StartsWith("ix_", StringComparison.Ordinal);
            Assert.True(isReportObject, $"{migration}: '{match.Value}' targets a schema other than report.");
        }

        Assert.DoesNotMatch(new Regex(@"\b(INSERT\s+INTO|UPDATE|DELETE\s+FROM)\s+(?!report\.)", RegexOptions.IgnoreCase), sql);
    }
}

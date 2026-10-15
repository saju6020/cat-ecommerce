using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-RPT-001 section 15 A -- U-A1, U-A4, U-A5, U-A6, U-A7, U-A8 and U-X1. Pure, no I/O.</summary>
public sealed class ReportingFoundationTests
{
    private static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    private static ReportDefinition Definition(
        string key = "test-report", ReportCategory? category = null, int maxDays = 366, bool bucketed = false, IReadOnlyList<ReportParameter>? parameters = null,
        IReadOnlyList<ReportColumn>? columns = null) =>
        new(key, "Test report", "A fixture.", category ?? ReportCategory.Financial, ReportKind.Period, ReportRelativePeriod.Today, maxDays,
            parameters ?? [], columns ?? [new ReportColumn("name", "Name", ReportColumnType.Text)], null, false, bucketed, ["test"]);

    // ── U-A1: every definition obeys the catalogue conventions (FR-RPT-1, US-RPT-01 AC2) ─────────────────────────
    [Fact]
    public void U_A1_EveryDefinition_HasAUniqueWellFormedKey_ACategoryAction_Columns_AndBothFormats()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in ReportCatalogue.All)
        {
            Assert.True(keys.Add(definition.Key), $"Duplicate key {definition.Key}");
            Assert.Matches(ReportRules.KeyPattern, definition.Key);
            Assert.InRange(definition.Key.Length, 1, ReportRules.KeyMaxLength);

            Assert.True(ActionRoles.TryGetRoles(ReportRules.ActionFor(definition.Category), out var roles), $"{definition.Key}: category action is not mapped");
            Assert.NotEmpty(roles);

            Assert.NotEmpty(definition.Columns);
            Assert.True(definition.MaxRangeDays > 0);
            Assert.Equal(definition.Columns.Count, definition.Columns.Select(c => c.Key).Distinct().Count());

            // Every definition takes from, to and branchId (FR-RPT-1) through the pipeline, so no parameter may shadow them.
            Assert.All(definition.Parameters, p => Assert.False(ReportFilterValidator.IsReserved(p.Name), $"{definition.Key}: parameter {p.Name} shadows a pipeline filter"));

            // A restricted column must name an action that exists.
            Assert.All(definition.Columns.Where(c => c.RestrictedToAction is not null), c => Assert.True(ActionRoles.TryGetRoles(c.RestrictedToAction!, out _)));
        }
    }

    [Fact]
    public void U_A1_ADuplicateKey_ThrowsWhenTheCatalogueIsBuilt()
    {
        Assert.Throws<InvalidOperationException>(() => ReportCatalogue.Build(typeof(DuplicateKeyCatalogue)));
    }

    [Fact]
    public void U_A1_AWronglyShapedContribution_ThrowsInsteadOfBeingSilentlySkipped()
    {
        Assert.Throws<InvalidOperationException>(() => ReportCatalogue.Build(typeof(WrongShapeCatalogue)));
    }

    private static class DuplicateKeyCatalogue
    {
#pragma warning disable CS0414
        private static readonly IReadOnlyList<ReportDefinition> OneDefinitions = [Definition("clash-key")];
        private static readonly IReadOnlyList<ReportDefinition> TwoDefinitions = [Definition("clash-key")];
#pragma warning restore CS0414
    }

    private static class WrongShapeCatalogue
    {
        public static readonly IReadOnlyList<ReportDefinition> PublicDefinitions = [Definition("public-key")];
    }

    // ── U-A4: range and parameter rules ────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_A4_NoRange_Inverted_TooLong_Unknown_AndBadParameters_AreRefusedWithTheirCodes()
    {
        var definition = Definition(maxDays: 366, parameters:
        [
            new ReportParameter("mode", "Mode", ReportParameterType.Enum, "Open", ["Open", "Closed"]),
            new ReportParameter("limit", "Limit", ReportParameterType.Integer, "10", Min: 1, Max: 50),
        ]);
        var none = new Dictionary<string, string>();
        var start = new DateOnly(2026, 1, 1);

        Assert.Equal(BusinessViolationCodes.ReportRangeRequired, ReportFilterValidator.Validate(definition, null, start, null, none).Violation!.Code);
        Assert.Equal(BusinessViolationCodes.ReportRangeRequired, ReportFilterValidator.Validate(definition, start, null, null, none).Violation!.Code);
        Assert.Equal(BusinessViolationCodes.ReportRangeInverted, ReportFilterValidator.Validate(definition, start, start.AddDays(-1), null, none).Violation!.Code);

        // 366 days inclusive is allowed; 367 is refused.
        Assert.Null(ReportFilterValidator.Validate(definition, start, start.AddDays(365), null, none).Violation);
        var tooLong = ReportFilterValidator.Validate(definition, start, start.AddDays(366), null, none).Violation!;
        Assert.Equal(BusinessViolationCodes.ReportRangeTooLong, tooLong.Code);
        Assert.Equal("This report covers at most 366 days. Choose a shorter date range.", tooLong.Message);

        Assert.Equal(BusinessViolationCodes.UnknownReportParameter,
            ReportFilterValidator.Validate(definition, start, start, null, new Dictionary<string, string> { ["nope"] = "1" }).Violation!.Code);
        Assert.Equal(BusinessViolationCodes.InvalidReportParameter,
            ReportFilterValidator.Validate(definition, start, start, null, new Dictionary<string, string> { ["mode"] = "Sideways" }).Violation!.Code);
        Assert.Equal(BusinessViolationCodes.InvalidReportParameter,
            ReportFilterValidator.Validate(definition, start, start, null, new Dictionary<string, string> { ["limit"] = "51" }).Violation!.Code);
    }

    [Fact]
    public void U_A4_Defaults_AreFilled_AndAnEnumValueIsCanonicalised()
    {
        var definition = Definition(parameters: [new ReportParameter("mode", "Mode", ReportParameterType.Enum, "Open", ["Open", "Closed"]), new ReportParameter("flag", "Flag", ReportParameterType.Boolean, "false")]);
        var day = new DateOnly(2026, 3, 3);

        var (filters, violation) = ReportFilterValidator.Validate(definition, day, day, null, new Dictionary<string, string> { ["MODE"] = "closed" });

        Assert.Null(violation);
        Assert.Equal("Closed", filters!.Parameters["mode"]);
        Assert.Equal(false, filters.Parameters["flag"]);
    }

    [Fact]
    public void U_A4_GroupBy_IsOnlyForTimeBucketedReports_AndDefaultsToDay()
    {
        var day = new DateOnly(2026, 3, 3);
        var none = new Dictionary<string, string>();

        Assert.Equal(BusinessViolationCodes.UnknownReportParameter, ReportFilterValidator.Validate(Definition(), day, day, "Week", none).Violation!.Code);

        var bucketed = Definition(bucketed: true);
        Assert.Equal(TimeBucket.Day, ReportFilterValidator.Validate(bucketed, day, day, null, none).Filters!.GroupBy);
        Assert.Equal(TimeBucket.Month, ReportFilterValidator.Validate(bucketed, day, day, "month", none).Filters!.GroupBy);
        Assert.Equal(BusinessViolationCodes.InvalidReportParameter, ReportFilterValidator.Validate(bucketed, day, day, "Fortnight", none).Violation!.Code);
    }

    // ── U-A5: a local date range is an instant range in the branch zone (ADR-282) ───────────────────────────────
    [Fact]
    public void U_A5_DhakaOctober_IsSixHoursAheadOfUtc()
    {
        var (start, end) = ReportPeriodResolver.ToInstants(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Dhaka);

        Assert.Equal(DateTimeOffset.Parse("2026-09-30T18:00:00Z"), start);
        Assert.Equal(DateTimeOffset.Parse("2026-10-31T18:00:00Z"), end);
    }

    [Fact]
    public void U_A5_LondonAcrossTheOctoberChange_HasATwentyFiveHourSunday()
    {
        // BST ends on Sunday 25 October 2026 at 02:00 (clocks go back): that day is 25 hours long.
        var (start, end) = ReportPeriodResolver.ToInstants(new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 25), London);

        Assert.Equal(DateTimeOffset.Parse("2026-10-24T23:00:00Z"), start);
        Assert.Equal(DateTimeOffset.Parse("2026-10-26T00:00:00Z"), end);
        Assert.Equal(TimeSpan.FromHours(25), end - start);
    }

    [Fact]
    public void U_A5_AllBranches_UsesTheMainBranchZone_NotTheCallers()
    {
        // The pipeline resolves the zone from the main branch for `all`; the resolver is zone-agnostic, so the same range in two zones differs by their offsets.
        var (dhakaStart, _) = ReportPeriodResolver.ToInstants(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), Dhaka);
        var (londonStart, _) = ReportPeriodResolver.ToInstants(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), London);

        Assert.NotEqual(dhakaStart, londonStart);
    }

    // ── U-A6: relative periods at 2026-10-14T03:00+06:00, a Wednesday ───────────────────────────────────────────
    [Theory]
    [InlineData("Today", "2026-10-14", "2026-10-14")]
    [InlineData("PreviousDay", "2026-10-13", "2026-10-13")]
    [InlineData("PreviousWeek", "2026-10-05", "2026-10-11")]
    [InlineData("PreviousMonth", "2026-09-01", "2026-09-30")]
    [InlineData("MonthToDate", "2026-10-01", "2026-10-14")]
    [InlineData("Last7Days", "2026-10-07", "2026-10-13")]
    [InlineData("Last30Days", "2026-09-14", "2026-10-13")]
    [InlineData("Next30Days", "2026-10-14", "2026-11-12")]
    [InlineData("Next90Days", "2026-10-14", "2027-01-11")]
    public void U_A6_RelativePeriods_ResolveInTheBranchZone(string period, string from, string to)
    {
        var asOf = DateTimeOffset.Parse("2026-10-14T03:00:00+06:00");

        var (resolvedFrom, resolvedTo) = ReportPeriodResolver.Resolve(Enumeration.GetAll<ReportRelativePeriod>().Single(p => p.DisplayName == period), asOf, Dhaka);

        Assert.Equal(DateOnly.Parse(from), resolvedFrom);
        Assert.Equal(DateOnly.Parse(to), resolvedTo);
    }

    [Fact]
    public void U_A6_ThreeInTheMorningInDhaka_IsStillYesterdayInUtc_ButTodayForTheClinic()
    {
        var asOf = DateTimeOffset.Parse("2026-10-14T03:00:00+06:00"); // 2026-10-13T21:00Z

        Assert.Equal(new DateOnly(2026, 10, 14), ReportPeriodResolver.LocalDate(asOf, Dhaka));
        Assert.Equal(new DateOnly(2026, 10, 13), ReportPeriodResolver.LocalDate(asOf, TimeZoneInfo.Utc));
    }

    // ── U-A7: who may open what, and for which branch ──────────────────────────────────────────────────────────
    [Theory]
    [InlineData(StaffRoles.Reception, "ClinicOperations", true)]
    [InlineData(StaffRoles.Reception, "Financial", false)]
    [InlineData(StaffRoles.Nurse, "ClinicOperations", false)]
    [InlineData(StaffRoles.Nurse, "Clinical", false)]
    [InlineData(StaffRoles.Nurse, "Stock", false)]
    [InlineData(StaffRoles.Vet, "ClinicOperations", true)] // OQ-7
    [InlineData(StaffRoles.Vet, "Clinical", true)]
    [InlineData(StaffRoles.Vet, "Financial", false)]
    [InlineData(StaffRoles.Accountant, "Financial", true)]
    [InlineData(StaffRoles.Accountant, "Clinical", false)]
    [InlineData(StaffRoles.ShopManager, "Sales", true)]
    [InlineData(StaffRoles.Accountant, "Sales", true)] // owner decision 2026-10-13
    [InlineData(StaffRoles.ShopManager, "Financial", false)]
    [InlineData(StaffRoles.Inventory, "Stock", true)]
    [InlineData(StaffRoles.Caretaker, "Boarding", true)]
    [InlineData(StaffRoles.Administrator, "Growth", true)]
    [InlineData(StaffRoles.Accountant, "Growth", false)]
    public void U_A7_TheCategoryAction_FollowsAppendixD(string role, string category, bool allowed)
    {
        var parsed = Enumeration.GetAll<ReportCategory>().Single(c => c.DisplayName == category);

        Assert.Equal(allowed, ReportAccessPolicy.CanOpen(parsed, [role]));
    }

    [Fact]
    public void U_A7_TheNurse_HoldsNoReportAction_AndTheMarkerIsEveryOtherStaffRole()
    {
        Assert.All(Enumeration.GetAll<ReportCategory>(), c => Assert.False(ReportAccessPolicy.CanOpen(c, [StaffRoles.Nurse])));
        Assert.False(ReportAccessPolicy.Holds([StaffRoles.Nurse], Actions.Reports.Any));
        Assert.All(StaffRoles.All.Where(r => r != StaffRoles.Nurse), r => Assert.True(ReportAccessPolicy.Holds([r], Actions.Reports.Any), r));
        Assert.All(StaffRoles.All, r => Assert.True(ReportAccessPolicy.Holds([r], Actions.Dashboard.View), r));
        Assert.Equal([StaffRoles.Administrator], ActionRoles.TryGetRoles(Actions.Reports.Schedule, out var schedulers) ? schedulers : []);
    }

    [Fact]
    public void U_A7_BranchScope_DefaultsToTheActiveBranch_AllNeedsACrossBranchRole_AndAnUnassignedBranchIsRefused()
    {
        var active = Guid.NewGuid();
        var other = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        IReadOnlyCollection<Guid> assigned = [active, other];

        var byDefault = ReportAccessPolicy.ResolveBranch(null, active, assigned, [StaffRoles.Reception]);
        Assert.Equal(active, byDefault.BranchId);

        Assert.Equal(other, ReportAccessPolicy.ResolveBranch(other.ToString(), active, assigned, [StaffRoles.Reception]).BranchId);

        Assert.Equal(BusinessViolationCodes.BranchNotAssigned, ReportAccessPolicy.ResolveBranch(stranger.ToString(), active, assigned, [StaffRoles.Administrator]).RefusalCode);
        Assert.Equal(BusinessViolationCodes.BranchNotAssigned, ReportAccessPolicy.ResolveBranch("not-a-guid", active, assigned, [StaffRoles.Administrator]).RefusalCode);
        Assert.Equal(BusinessViolationCodes.BranchNotAssigned, ReportAccessPolicy.ResolveBranch(null, null, assigned, [StaffRoles.Reception]).RefusalCode);

        Assert.True(ReportAccessPolicy.ResolveBranch("all", active, assigned, [StaffRoles.Accountant]).IsAllBranches);
        Assert.True(ReportAccessPolicy.ResolveBranch("ALL", active, assigned, [StaffRoles.Administrator]).IsAllBranches);
        Assert.Equal(BusinessViolationCodes.CrossBranchReportNotPermitted, ReportAccessPolicy.ResolveBranch("all", active, assigned, [StaffRoles.Reception]).RefusalCode);
        Assert.Equal(BusinessViolationCodes.CrossBranchReportNotPermitted, ReportAccessPolicy.ResolveBranch("all", active, assigned, [StaffRoles.Reception, StaffRoles.Vet]).RefusalCode);
    }

    // ── U-A8 / U-G4: a restricted column is removed, and a recipient list sees only the intersection ───────────
    private static ReportDefinition RestrictedDefinition() => Definition(columns:
    [
        new ReportColumn("category", "Category", ReportColumnType.Text),
        new ReportColumn("netRevenue", "Net revenue", ReportColumnType.Money),
        new ReportColumn("cost", "Cost", ReportColumnType.Money, RestrictedToAction: Actions.Reports.FinancialView),
        new ReportColumn("margin", "Margin", ReportColumnType.Money, RestrictedToAction: Actions.Reports.FinancialView),
    ]);

    [Fact]
    public void U_A8_AShopManager_DoesNotSeeCostOrMargin_AnAccountantSeesEverything()
    {
        var definition = RestrictedDefinition();

        Assert.Equal(["category", "netRevenue"], ReportAccessPolicy.VisibleColumns(definition, [StaffRoles.ShopManager]).Select(c => c.Key));
        Assert.Equal(["category", "netRevenue", "cost", "margin"], ReportAccessPolicy.VisibleColumns(definition, [StaffRoles.Accountant]).Select(c => c.Key));
        Assert.Equal(4, ReportAccessPolicy.VisibleColumns(definition, [StaffRoles.Administrator]).Count);
    }

    [Fact]
    public void U_G4_TheColumnsEveryRecipientMaySee_AreTheIntersection()
    {
        var definition = RestrictedDefinition();
        IReadOnlyCollection<string> shop = [StaffRoles.ShopManager];
        IReadOnlyCollection<string> accountant = [StaffRoles.Accountant];
        IReadOnlyCollection<string> admin = [StaffRoles.Administrator];

        Assert.Equal(["category", "netRevenue"], ReportAccessPolicy.VisibleColumnsForAll(definition, [shop, accountant]).Select(c => c.Key));
        Assert.Equal(4, ReportAccessPolicy.VisibleColumnsForAll(definition, [accountant, admin]).Count);
    }

    // ── U-X1: the three settings ───────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_X1_TheReportingSettings_AreRegistered_WithDefaultsInsideTheirBounds_AndNoBranchOverride()
    {
        var keys = new[] { SettingKeys.ReportingExportRetentionDays, SettingKeys.ReportingMaxAttachmentMegabytes, SettingKeys.ReportingDashboardRefreshSeconds };

        Assert.Equal(keys.Length, keys.Distinct().Count());
        foreach (var key in keys)
        {
            var definition = SettingDefinitions.Find(key);
            Assert.NotNull(definition);
            Assert.Equal("Reporting", definition!.Group);
            Assert.False(definition.BranchOverridable);
            var value = int.Parse(definition.Default);
            Assert.InRange(value, int.Parse(definition.Min!), int.Parse(definition.Max!));
        }

        Assert.Equal("7", SettingDefinitions.Find(SettingKeys.ReportingExportRetentionDays)!.Default);
        Assert.Equal("10", SettingDefinitions.Find(SettingKeys.ReportingMaxAttachmentMegabytes)!.Default);
        Assert.Equal("60", SettingDefinitions.Find(SettingKeys.ReportingDashboardRefreshSeconds)!.Default);
    }
}

using CatEcommerce.Domain.Aggregates.Reporting;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Common.Security;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-RPT-001 section 15 G -- U-G1 (the calculator), U-G2 (idempotent completion and missed slots), U-G3 (invariants), U-G4 (visible-column intersection) and U-G6 (the template). Pure.</summary>
public sealed class ReportScheduleDomainTests
{
    private static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
    private static readonly UserContext Actor = new(Guid.NewGuid(), Guid.Empty);

    private static DateTimeOffset Utc(string text) => DateTimeOffset.Parse(text, null, System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime();

    // ── U-G1 ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_G1_Daily0700Dhaka_IsTheNextSevenInTheMorning_AndNeverTheOneAlreadyPassed()
    {
        var seven = new TimeOnly(7, 0);
        Assert.Equal(Utc("2026-10-13T01:00:00Z"), ReportScheduleCalculator.NextAfter(ReportFrequency.Daily, null, null, false, seven, Utc("2026-10-13T00:00:00Z"), Dhaka));
        Assert.Equal(Utc("2026-10-14T01:00:00Z"), ReportScheduleCalculator.NextAfter(ReportFrequency.Daily, null, null, false, seven, Utc("2026-10-13T01:00:00Z"), Dhaka));
        Assert.Equal(Utc("2026-10-14T01:00:00Z"), ReportScheduleCalculator.NextAfter(ReportFrequency.Daily, null, null, false, seven, Utc("2026-10-13T02:00:00Z"), Dhaka));
    }

    [Fact]
    public void U_G1_WeeklyMonday_AndMonthlyThe28th_AndTheLastDayOfFebruary2027()
    {
        var nine = new TimeOnly(9, 0);
        // Tuesday 13 Oct 2026 -> Monday 19 Oct, 09:00 Dhaka (+06:00).
        Assert.Equal(Utc("2026-10-19T03:00:00Z"), ReportScheduleCalculator.NextAfter(ReportFrequency.Weekly, DayOfWeek.Monday, null, false, nine, Utc("2026-10-13T08:00:00Z"), Dhaka));
        Assert.Equal(Utc("2026-11-28T03:00:00Z"), ReportScheduleCalculator.NextAfter(ReportFrequency.Monthly, null, 28, false, nine, Utc("2026-10-29T08:00:00Z"), Dhaka));
        // 2027 is not a leap year: the last day of February is the 28th.
        Assert.Equal(Utc("2027-02-28T03:00:00Z"), ReportScheduleCalculator.NextAfter(ReportFrequency.Monthly, null, null, true, nine, Utc("2027-02-01T00:00:00Z"), Dhaka));
        // 2028 is: the 29th.
        Assert.Equal(Utc("2028-02-29T03:00:00Z"), ReportScheduleCalculator.NextAfter(ReportFrequency.Monthly, null, null, true, nine, Utc("2028-02-01T00:00:00Z"), Dhaka));
    }

    [Fact]
    public void U_G1_London0130_OnSpringForwardDay_MovesToTheFirstValidInstant_0200BST()
    {
        // 29 Mar 2026: 01:00 GMT jumps to 02:00 BST, so 01:30 does not exist.
        var next = ReportScheduleCalculator.NextAfter(ReportFrequency.Daily, null, null, false, new TimeOnly(1, 30), Utc("2026-03-28T12:00:00Z"), London);

        Assert.Equal(Utc("2026-03-29T01:00:00Z"), next);
        Assert.Equal(new TimeOnly(2, 0), TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(next, London).DateTime));
    }

    [Fact]
    public void U_G1_London0130_OnFallBackDay_TakesTheFirstOccurrence_BST()
    {
        // 25 Oct 2026: 02:00 BST falls back to 01:00 GMT, so 01:30 happens twice; the first (BST, 00:30Z) wins.
        var next = ReportScheduleCalculator.NextAfter(ReportFrequency.Daily, null, null, false, new TimeOnly(1, 30), Utc("2026-10-24T12:00:00Z"), London);
        Assert.Equal(Utc("2026-10-25T00:30:00Z"), next);

        // And the day after is an ordinary GMT 01:30.
        Assert.Equal(Utc("2026-10-26T01:30:00Z"), ReportScheduleCalculator.NextAfter(ReportFrequency.Daily, null, null, false, new TimeOnly(1, 30), next, London));
    }

    [Fact]
    public void U_G1_London0700_IsAnHourLaterInUtcAfterTheOctoberChange()
    {
        var seven = new TimeOnly(7, 0);
        var friday = ReportScheduleCalculator.NextAfter(ReportFrequency.Daily, null, null, false, seven, Utc("2026-10-23T08:00:00Z"), London);
        var saturday = ReportScheduleCalculator.NextAfter(ReportFrequency.Daily, null, null, false, seven, friday, London);

        Assert.Equal(Utc("2026-10-24T06:00:00Z"), friday);   // BST (+01:00) on the 24th
        Assert.Equal(Utc("2026-10-25T07:00:00Z"), saturday); // GMT after the change on the 25th: the same 07:00 local, an hour later in UTC
    }

    // ── U-G2 ────────────────────────────────────────────────────────────────────────────────────────

    private static ReportDefinition Definition(string key = ReportCatalogue.RevenueByBusinessLineKey) => ReportCatalogue.Find(key)!;

    private static ReportScheduleDto Dto(
        DateTimeOffset now, ReportDefinition? definition = null, ReportRelativePeriod? period = null, ReportFrequency? frequency = null, DayOfWeek? dayOfWeek = null, int? dayOfMonth = null,
        bool lastDay = false, TimeOnly? sendAt = null, IReadOnlyList<Guid>? recipients = null, string name = "Daily revenue") =>
        new(name, definition ?? Definition(), null, period ?? ReportRelativePeriod.PreviousDay, new Dictionary<string, string>(), ReportFormat.Csv, frequency ?? ReportFrequency.Daily, dayOfWeek,
            dayOfMonth, lastDay, sendAt ?? new TimeOnly(7, 0), recipients ?? [Guid.NewGuid()], Dhaka, now, Actor);

    [Fact]
    public void U_G2_RecordRunCompleted_CalledTwiceForOneDueTime_IsANoOp_TheSecondTime()
    {
        var now = Utc("2026-10-13T00:00:00Z");
        var schedule = new ReportScheduleAggregate(Guid.NewGuid());
        schedule.Create(Dto(now));
        var due = schedule.NextRunOn!.Value;

        schedule.RecordRunCompleted(due, due.AddSeconds(5), Dhaka, Actor);
        var versionAfterFirst = schedule.Version;
        var next = schedule.NextRunOn;
        schedule.RecordRunCompleted(due, due.AddSeconds(30), Dhaka, Actor);

        Assert.Equal(versionAfterFirst, schedule.Version);
        Assert.Equal(next, schedule.NextRunOn);
        Assert.Equal(due, schedule.LastRunDueOn);
        Assert.Equal(Utc("2026-10-14T01:00:00Z"), schedule.NextRunOn);
    }

    [Fact]
    public void U_G2_AfterThreeDaysOfDowntime_ThereIsOneNextSlotAfterNow_MissedSlotsCollapse()
    {
        var created = Utc("2026-10-13T00:00:00Z");
        var schedule = new ReportScheduleAggregate(Guid.NewGuid());
        schedule.Create(Dto(created));
        var due = schedule.NextRunOn!.Value; // 13 Oct 01:00Z

        var now = Utc("2026-10-16T03:00:00Z"); // 16 Oct, 09:00 Dhaka: the 13th, 14th, 15th and 16th slots are all behind us
        schedule.RecordRunCompleted(due, now, Dhaka, Actor);

        Assert.Equal(Utc("2026-10-17T01:00:00Z"), schedule.NextRunOn);
        Assert.Equal(now, schedule.LastRunOn);
    }

    [Fact]
    public void ResumeRecomputesFromNow_NoCatchUp_AndACancelledScheduleNeverRunsAgain()
    {
        var schedule = new ReportScheduleAggregate(Guid.NewGuid());
        schedule.Create(Dto(Utc("2026-10-13T00:00:00Z")));
        schedule.Pause(Actor);
        Assert.Null(schedule.NextRunOn);

        schedule.Resume(Utc("2026-10-20T03:00:00Z"), Dhaka, Actor);
        Assert.Equal(Utc("2026-10-21T01:00:00Z"), schedule.NextRunOn);

        schedule.Cancel(Actor);
        Assert.Null(schedule.NextRunOn);
        Assert.False(schedule.IsDue(Utc("2027-01-01T00:00:00Z")));
        var ex = Assert.Throws<DomainRuleViolationException>(() => schedule.Resume(Utc("2026-10-22T00:00:00Z"), Dhaka, Actor));
        Assert.Equal(BusinessViolationCodes.ReportScheduleCancelled, ex.ErrorCode);
    }

    // ── U-G3 ────────────────────────────────────────────────────────────────────────────────────────

    private static string CodeOf(ReportScheduleDto dto) =>
        Assert.Throws<DomainRuleViolationException>(() => new ReportScheduleAggregate(Guid.NewGuid()).Create(dto)).ErrorCode;

    [Fact]
    public void U_G3_Recipients_ZeroTwentyOneAndDuplicate_EachNamesItsCode()
    {
        var now = Utc("2026-10-13T00:00:00Z");
        Assert.Equal(BusinessViolationCodes.ReportRecipientRequired, CodeOf(Dto(now, recipients: [])));
        Assert.Equal(BusinessViolationCodes.ReportRecipientLimitExceeded, CodeOf(Dto(now, recipients: Enumerable.Range(0, ReportScheduleRules.MaxRecipients + 1).Select(_ => Guid.NewGuid()).ToList())));

        var duplicate = Guid.NewGuid();
        Assert.Equal(BusinessViolationCodes.InvalidReportSchedule, CodeOf(Dto(now, recipients: [duplicate, duplicate])));

        // The upper bound itself is allowed.
        new ReportScheduleAggregate(Guid.NewGuid()).Create(Dto(now, recipients: Enumerable.Range(0, ReportScheduleRules.MaxRecipients).Select(_ => Guid.NewGuid()).ToList()));
    }

    [Fact]
    public void U_G3_Frequency_DayOfMonth29_BothMonthlyOptions_AWeeklyWithoutADay_AndTheFiveMinuteGrid()
    {
        var now = Utc("2026-10-13T00:00:00Z");
        Assert.Equal(BusinessViolationCodes.InvalidReportSchedule, CodeOf(Dto(now, frequency: ReportFrequency.Monthly, dayOfMonth: 29)));
        Assert.Equal(BusinessViolationCodes.InvalidReportSchedule, CodeOf(Dto(now, frequency: ReportFrequency.Monthly, dayOfMonth: 5, lastDay: true)));
        Assert.Equal(BusinessViolationCodes.InvalidReportSchedule, CodeOf(Dto(now, frequency: ReportFrequency.Monthly)));
        Assert.Equal(BusinessViolationCodes.InvalidReportSchedule, CodeOf(Dto(now, frequency: ReportFrequency.Weekly)));
        Assert.Equal(BusinessViolationCodes.InvalidReportSchedule, CodeOf(Dto(now, sendAt: new TimeOnly(7, 3))));
        Assert.Equal(BusinessViolationCodes.InvalidReportSchedule, CodeOf(Dto(now, name: "  ")));
        Assert.Equal(BusinessViolationCodes.InvalidReportSchedule, CodeOf(Dto(now, name: new string('x', ReportScheduleRules.NameMaxLength + 1))));

        new ReportScheduleAggregate(Guid.NewGuid()).Create(Dto(now, frequency: ReportFrequency.Monthly, dayOfMonth: 28));
        new ReportScheduleAggregate(Guid.NewGuid()).Create(Dto(now, frequency: ReportFrequency.Monthly, lastDay: true));
    }

    [Fact]
    public void U_G3_APeriodThatDoesNotSuitTheReport_IsRefused_WithItsOwnCode()
    {
        var now = Utc("2026-10-13T00:00:00Z");
        var asAt = ReportCatalogue.All.First(d => d.Kind == ReportKind.AsAt && !IsForward(d.DefaultPeriod));
        var period = ReportCatalogue.All.First(d => d.Kind == ReportKind.Period && d.DefaultPeriod != ReportRelativePeriod.Today && !IsForward(d.DefaultPeriod));
        var forward = ReportCatalogue.All.First(d => IsForward(d.DefaultPeriod));

        // An as-at report does not take a backward window; a period report does not take "today" unless that is its own default; a backward report does not take a forward window.
        Assert.Equal(BusinessViolationCodes.InvalidReportSchedulePeriod, CodeOf(Dto(now, definition: asAt, period: ReportRelativePeriod.PreviousMonth)));
        Assert.Equal(BusinessViolationCodes.InvalidReportSchedulePeriod, CodeOf(Dto(now, definition: period, period: ReportRelativePeriod.Today)));
        Assert.Equal(BusinessViolationCodes.InvalidReportSchedulePeriod, CodeOf(Dto(now, definition: period, period: ReportRelativePeriod.Next30Days)));
        Assert.Equal(BusinessViolationCodes.InvalidReportSchedulePeriod, CodeOf(Dto(now, definition: forward, period: ReportRelativePeriod.PreviousDay)));

        new ReportScheduleAggregate(Guid.NewGuid()).Create(Dto(now, definition: asAt, period: ReportRelativePeriod.Today));
        new ReportScheduleAggregate(Guid.NewGuid()).Create(Dto(now, definition: period, period: ReportRelativePeriod.PreviousMonth));
        new ReportScheduleAggregate(Guid.NewGuid()).Create(Dto(now, definition: forward, period: forward.DefaultPeriod));
    }

    private static ReportDefinition Shaped(ReportKind kind, ReportRelativePeriod defaultPeriod) =>
        new("unit-report", "Unit report", "d", ReportCategory.Financial, kind, defaultPeriod, 366, [], [new ReportColumn("a", "A", ReportColumnType.Text)], null, false, false, []);

    /// <summary>The kind x period matrix of <see cref="ReportSchedulePeriods.IsCompatible"/>, directly (it was only covered through the aggregate's violations).</summary>
    [Theory]
    // a period report with a backward default: any backward window, "today" only when that is its own default, never a forward window
    [InlineData("Period", "PreviousMonth", "PreviousDay", true)]
    [InlineData("Period", "PreviousMonth", "MonthToDate", true)]
    [InlineData("Period", "PreviousMonth", "Last30Days", true)]
    [InlineData("Period", "PreviousMonth", "Today", false)]
    [InlineData("Period", "PreviousMonth", "Next30Days", false)]
    [InlineData("Period", "Today", "Today", true)]
    [InlineData("Period", "Today", "PreviousWeek", true)]
    // an as-at report: today or its own default, nothing else
    [InlineData("AsAt", "Today", "Today", true)]
    [InlineData("AsAt", "Today", "PreviousMonth", false)]
    [InlineData("AsAt", "Last7Days", "Last7Days", true)]
    [InlineData("AsAt", "Last7Days", "Today", true)]
    [InlineData("AsAt", "Last7Days", "Last30Days", false)]
    // a forward-looking report takes only a forward window
    [InlineData("Period", "Next30Days", "Next30Days", true)]
    [InlineData("Period", "Next30Days", "Next90Days", true)]
    [InlineData("Period", "Next30Days", "PreviousDay", false)]
    [InlineData("Period", "Next30Days", "Today", false)]
    [InlineData("AsAt", "Next90Days", "Next30Days", true)]
    [InlineData("AsAt", "Next90Days", "Today", false)]
    public void IsCompatible_FollowsTheKindByPeriodMatrix(string kind, string defaultPeriod, string period, bool expected)
    {
        var definition = Shaped(Enumeration.GetAll<ReportKind>().Single(k => k.DisplayName == kind), Enumeration.GetAll<ReportRelativePeriod>().Single(p => p.DisplayName == defaultPeriod));

        Assert.Equal(expected, ReportSchedulePeriods.IsCompatible(definition, Enumeration.GetAll<ReportRelativePeriod>().Single(p => p.DisplayName == period)));
    }

    [Fact]
    public void CanReceive_NeedsTheCategoryAction_AndAllBranchesNeedsACrossBranchRole_AndABranchNeedsTheAssignment()
    {
        var branch = Guid.NewGuid();
        var definition = ReportCatalogue.Find(ReportCatalogue.ClinicDailyScheduleKey)!;

        Assert.True(ReportAccessPolicy.CanReceive(definition, [StaffRoles.Reception], branch, [branch]));
        Assert.False(ReportAccessPolicy.CanReceive(definition, [StaffRoles.Reception], branch, [Guid.NewGuid()]));   // no longer assigned
        Assert.False(ReportAccessPolicy.CanReceive(definition, [StaffRoles.Reception], null, [branch]));              // all branches needs a cross-branch role
        Assert.True(ReportAccessPolicy.CanReceive(definition, [StaffRoles.Administrator], null, []));
        Assert.False(ReportAccessPolicy.CanReceive(definition, [StaffRoles.Nurse], branch, [branch]));               // no category action
        Assert.False(ReportAccessPolicy.CanReceive(ReportCatalogue.Find(ReportCatalogue.RevenueByBusinessLineKey)!, [StaffRoles.Reception], branch, [branch]));
    }

    private static bool IsForward(ReportRelativePeriod period) => period == ReportRelativePeriod.Next30Days || period == ReportRelativePeriod.Next90Days;

    // ── U-G4 ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_G4_TheColumnsEveryRecipientMaySee_AreTheIntersection_NobodyReceivesWhatTheyCouldNotOpen()
    {
        var shop = Definition(ReportCatalogue.RevenueShopByCategoryKey);
        var restricted = shop.Columns.Where(c => c.RestrictedToAction is not null).Select(c => c.Key).ToList();
        Assert.NotEmpty(restricted);

        var shopManager = new[] { StaffRoles.ShopManager };
        var accountant = new[] { StaffRoles.Accountant };
        var administrator = new[] { StaffRoles.Administrator };

        // A ShopManager has no financial action: the cost columns drop for everyone on the schedule.
        var withShopManager = ReportAccessPolicy.VisibleColumnsForAll(shop, [shopManager, administrator]).Select(c => c.Key).ToList();
        Assert.Empty(withShopManager.Intersect(restricted));
        Assert.Equal(shop.Columns.Where(c => c.RestrictedToAction is null).Select(c => c.Key).OrderBy(k => k), withShopManager.OrderBy(k => k));

        var withAccountant = ReportAccessPolicy.VisibleColumnsForAll(shop, [shopManager, accountant]).Select(c => c.Key).ToList();
        Assert.Empty(withAccountant.Intersect(restricted));

        // Accountant and Administrator both hold the financial action: every column.
        var all = ReportAccessPolicy.VisibleColumnsForAll(shop, [accountant, administrator]).Select(c => c.Key).ToList();
        Assert.Equal(shop.Columns.Select(c => c.Key).OrderBy(k => k), all.OrderBy(k => k));
    }

    // ── U-G6 ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_G6_TheScheduledDeliveryTemplate_PassesTemplateRules_IsEmailOnly_Transactional_Normal_NotSecret_WithARequiredLink()
    {
        var definition = NotificationTemplateCatalogue.Definitions.Single(d => d.Key == NotificationTemplateCatalogue.ReportScheduledDelivery);
        var contents = definition.Defaults.Select(d => new TemplateContentInput(d.Channel, d.Subject, d.Title, d.Body, d.LinkPath)).ToList();

        Assert.Null(TemplateRules.Validate(definition, contents));
        Assert.Equal([NotificationChannel.Email], definition.Channels);
        Assert.Equal(NotificationCategory.Transactional, definition.Category);
        Assert.Equal(NotificationUrgency.Normal, definition.Urgency);
        Assert.False(definition.ContainsSecret);

        var link = definition.Variables.Single(v => v.Name == "reportLink");
        Assert.Equal(TemplateVariableKind.Url, link.Kind);
        Assert.Equal([NotificationChannel.Email], link.RequiredOn);

        var names = definition.Variables.Select(v => v.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "attachmentNote", "branchName", "generatedOnLocal", "periodLabel", "reportLink", "reportTitle", "rowCount" }, names);
    }

    [Fact]
    public void TheScheduledReportSubjectKind_IsAppended_AsValueTen()
    {
        Assert.Equal(10, NotificationSubjectKind.ScheduledReport.Value);
        Assert.Equal(1, NotificationAttachmentKind.ReportRun.Value);
    }
}

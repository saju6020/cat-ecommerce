using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.Domain.Tests.Surgery;

/// <summary>HO-SRG-001 §8.2 / §11.4 C12 -- U-E3..U-E6: the surgical report, immutable once finalised, extended only by addenda that leave the root untouched.</summary>
public sealed class SurgicalReportAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 6, 0, 0, TimeSpan.Zero);
    private static readonly Guid Surgeon = Guid.NewGuid();
    private static readonly Guid Assistant = Guid.NewGuid();

    private static SurgicalReportDraftDto Draft(string suture = "4-0 PDS", string procedure = "Ovariohysterectomy", string findings = "Normal", string technique = "Midline",
        bool complications = false, string? complicationText = null, Guid? surgeon = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), procedure, findings, technique, suture, complications, complicationText, surgeon ?? Surgeon, Assistant, TestUserContext.Create(), Guid.NewGuid());

    private static SurgicalReportAggregate Finalised()
    {
        var report = new SurgicalReportAggregate(Guid.NewGuid());
        report.SaveDraft(Draft());
        report.Finalise(new SurgicalReportFinaliseDto(true, Now, TestUserContext.Create(), Guid.NewGuid()));
        return report;
    }

    /// <summary>U-E3 -- finalising with the suture material empty, and with complications flagged but empty: ReportIncomplete naming the fields.</summary>
    [Fact]
    public void U_E3_Finalise_NamesTheMissingFields()
    {
        var noSuture = new SurgicalReportAggregate(Guid.NewGuid());
        noSuture.SaveDraft(Draft(suture: ""));
        var ex1 = Assert.Throws<DomainRuleViolationException>(() => noSuture.Finalise(new SurgicalReportFinaliseDto(true, Now, TestUserContext.Create(), Guid.NewGuid())));
        Assert.Equal(BusinessViolationCodes.ReportIncomplete, ex1.ErrorCode);
        Assert.Contains("suture material", ex1.Message);
        Assert.False(noSuture.IsFinalised);

        var noComplications = new SurgicalReportAggregate(Guid.NewGuid());
        noComplications.SaveDraft(Draft(complications: true, complicationText: "  "));
        var ex2 = Assert.Throws<DomainRuleViolationException>(() => noComplications.Finalise(new SurgicalReportFinaliseDto(true, Now, TestUserContext.Create(), Guid.NewGuid())));
        Assert.Contains("complications", ex2.Message);

        var several = new SurgicalReportAggregate(Guid.NewGuid());
        several.SaveDraft(Draft(suture: "", procedure: "", findings: "", surgeon: Guid.Empty));
        var ex3 = Assert.Throws<DomainRuleViolationException>(() => several.Finalise(new SurgicalReportFinaliseDto(true, Now, TestUserContext.Create(), Guid.NewGuid())));
        Assert.Contains("procedure performed", ex3.Message);
        Assert.Contains("findings", ex3.Message);
        Assert.Contains("suture material", ex3.Message);
        Assert.Contains("surgeon", ex3.Message);
        Assert.DoesNotContain("technique", ex3.Message);
    }

    /// <summary>U-E4 -- a draft saved after finalisation: ReportNotEditable.</summary>
    [Fact]
    public void U_E4_ADraft_AfterFinalisation_IsNotEditable()
    {
        var report = Finalised();

        var ex = Assert.Throws<DomainRuleViolationException>(() => report.SaveDraft(Draft(findings: "Changed my mind")));

        Assert.Equal(BusinessViolationCodes.ReportNotEditable, ex.ErrorCode);
        Assert.Equal("Normal", report.Findings);
        Assert.Equal(BusinessViolationCodes.ReportAlreadyFinalised,
            Assert.Throws<DomainRuleViolationException>(() => report.Finalise(new SurgicalReportFinaliseDto(true, Now, TestUserContext.Create(), Guid.NewGuid()))).ErrorCode);
    }

    /// <summary>U-E5 -- a nurse cannot finalise (BR-6); neither can they add an addendum.</summary>
    [Fact]
    public void U_E5_OnlyAVet_Finalises_AndAddsAnAddendum()
    {
        var report = new SurgicalReportAggregate(Guid.NewGuid());
        report.SaveDraft(Draft());

        var ex = Assert.Throws<DomainRuleViolationException>(() => report.Finalise(new SurgicalReportFinaliseDto(false, Now, TestUserContext.Create(), Guid.NewGuid())));
        Assert.Equal(BusinessViolationCodes.VeterinarianRequired, ex.ErrorCode);
        Assert.False(report.IsFinalised);

        var finalised = Finalised();
        Assert.Equal(BusinessViolationCodes.VeterinarianRequired,
            Assert.Throws<DomainRuleViolationException>(() => finalised.AddAddendum(new SurgicalReportAddendumDto(Guid.NewGuid(), "Suture count confirmed", false, Now, TestUserContext.Create(), Guid.NewGuid()))).ErrorCode);
    }

    /// <summary>U-E6 -- an addendum to a finalised report adds the addendum row and leaves the ROOT untouched: the version does not change (the trigger would fire on a root update).</summary>
    [Fact]
    public void U_E6_AnAddendum_AddsARow_AndLeavesTheRootVersionUnchanged()
    {
        var report = Finalised();
        var version = report.Version;
        var lastUpdated = report.LastUpdatedDate;
        var addendumId = Guid.NewGuid();

        report.AddAddendum(new SurgicalReportAddendumDto(addendumId, "  Owner informed of the findings by phone.  ", true, Now.AddHours(1), TestUserContext.Create(), Guid.NewGuid()));

        var addendum = Assert.Single(report.Addenda);
        Assert.Equal(addendumId, addendum.Id);
        Assert.Equal("Owner informed of the findings by phone.", addendum.Text);
        Assert.Equal(Now.AddHours(1), addendum.AddedOn);
        Assert.Equal(version, report.Version);
        Assert.Equal(lastUpdated, report.LastUpdatedDate);
        Assert.Contains(report.Events, e => e is SurgicalReportAddendumAddedEvent added && added.AddendumId == addendumId);
    }

    [Fact]
    public void AnAddendum_NeedsAFinalisedReport_AndText()
    {
        var draft = new SurgicalReportAggregate(Guid.NewGuid());
        draft.SaveDraft(Draft());
        Assert.Equal(BusinessViolationCodes.ReportNotFinalised,
            Assert.Throws<DomainRuleViolationException>(() => draft.AddAddendum(new SurgicalReportAddendumDto(Guid.NewGuid(), "x", true, Now, TestUserContext.Create(), Guid.NewGuid()))).ErrorCode);

        var report = Finalised();
        Assert.Equal(BusinessViolationCodes.SurgeryTextInvalid,
            Assert.Throws<DomainRuleViolationException>(() => report.AddAddendum(new SurgicalReportAddendumDto(Guid.NewGuid(), " ", true, Now, TestUserContext.Create(), Guid.NewGuid()))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.SurgeryTextInvalid,
            Assert.Throws<DomainRuleViolationException>(() => report.AddAddendum(new SurgicalReportAddendumDto(Guid.NewGuid(), new string('x', SurgeryRules.AddendumMax + 1), true, Now, TestUserContext.Create(), Guid.NewGuid()))).ErrorCode);
    }

    [Fact]
    public void ADraft_IsReplacedOnEverySave_AndComplicationsAreDroppedWhenNoneAreFlagged()
    {
        var report = new SurgicalReportAggregate(Guid.NewGuid());

        report.SaveDraft(Draft(complications: true, complicationText: "Bleeding from the ovarian pedicle"));
        Assert.Equal("Bleeding from the ovarian pedicle", report.Complications);

        report.SaveDraft(Draft(complications: false, complicationText: "stale text"));
        Assert.Null(report.Complications);
        Assert.False(report.HadComplications);
        Assert.False(report.IsFinalised);
    }
}

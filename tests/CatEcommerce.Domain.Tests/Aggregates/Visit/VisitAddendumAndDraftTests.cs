using System.Reflection;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Tests.Aggregates.Visit;

/// <summary>HO-EMR-001 §15 U-C8, U-C9 (addenda) and the draft working copy rules (ADR-122).</summary>
public sealed class VisitAddendumAndDraftTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    private static VisitAddendumDto Addendum(VisitStatus status, string text = "Corrected the weight.", string? reason = "Typo") =>
        new(Guid.NewGuid(), Guid.NewGuid(), status, text, reason, Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());

    [Fact]
    public void Add_OnAnOpenVisit_IsRefused()
    {
        var addendum = new VisitAddendumAggregate(Guid.NewGuid());

        var violation = Assert.Throws<DomainRuleViolationException>(() => addendum.Add(Addendum(VisitStatus.Open)));

        Assert.Equal(BusinessViolationCodes.AddendumRequiresFinalisedVisit, violation.ErrorCode);
        Assert.Contains("edit the note instead", violation.Message);
    }

    [Fact]
    public void Add_OnAFinalisedVisit_RecordsAuthorAndTime()
    {
        var addendum = new VisitAddendumAggregate(Guid.NewGuid());
        var dto = Addendum(VisitStatus.Finalised);

        addendum.Add(dto);

        Assert.Equal(dto.VisitId, addendum.VisitId);
        Assert.Equal(dto.AuthorStaffId, addendum.AuthorStaffId);
        Assert.Equal(Now, addendum.AddedOn);
        Assert.Equal("Corrected the weight.", addendum.Text);
        Assert.Single(addendum.Events.OfType<VisitAddendumAddedEvent>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_BlankText_IsRefused(string text)
    {
        var addendum = new VisitAddendumAggregate(Guid.NewGuid());
        Assert.Throws<DomainRuleViolationException>(() => addendum.Add(Addendum(VisitStatus.Finalised, text)));
    }

    [Fact]
    public void Add_TextOverTheLimit_IsRefused()
    {
        var addendum = new VisitAddendumAggregate(Guid.NewGuid());
        var text = new string('x', ClinicalNoteRules.MaxAddendumLength + 1);

        Assert.Throws<DomainRuleViolationException>(() => addendum.Add(Addendum(VisitStatus.Finalised, text)));
    }

    /// <summary>U-C9 -- no public method besides <c>Add</c>: there is no edit, remove or amend path.</summary>
    [Fact]
    public void VisitAddendumAggregate_HasNoPublicMethodBesidesAdd()
    {
        var declared = typeof(VisitAddendumAggregate)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name);

        Assert.Equal([nameof(VisitAddendumAggregate.Add)], declared);
    }

    // ── draft ──────────────────────────────────────────────────────────────────────────────────

    private static ConsultationDraftDto Draft(VisitStatus status, string subjective = "S", int problems = 0, DateTimeOffset? at = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), status, subjective, "O", "A", "P", "D",
        Enumerable.Range(0, problems).Select(_ => Guid.NewGuid()).ToList(), at ?? Now, TestUserContext.Create(), Guid.NewGuid());

    [Fact]
    public void Overwrite_CreatesThenReplacesTheWorkingCopy()
    {
        var draft = new ConsultationDraftAggregate(Guid.NewGuid());
        var first = Draft(VisitStatus.Open, "first");

        draft.Overwrite(first);
        draft.Overwrite(first with { Subjective = "second", Now = Now.AddMinutes(1) });

        Assert.Equal("second", draft.Subjective);
        Assert.Equal(Now.AddMinutes(1), draft.SavedOn);
        Assert.Equal(first.VisitId, draft.VisitId);
        Assert.Equal(first.AuthorStaffId, draft.AuthorStaffId);
    }

    [Fact]
    public void Overwrite_OnAFinalisedVisit_IsRefused()
    {
        var draft = new ConsultationDraftAggregate(Guid.NewGuid());
        Assert.Equal(BusinessViolationCodes.VisitFinalised,
            Assert.Throws<DomainRuleViolationException>(() => draft.Overwrite(Draft(VisitStatus.Finalised))).ErrorCode);
    }

    [Fact]
    public void Overwrite_TwentyOneProblemCodes_OrAnOversizedField_IsRefused()
    {
        var draft = new ConsultationDraftAggregate(Guid.NewGuid());

        Assert.Throws<DomainRuleViolationException>(() => draft.Overwrite(Draft(VisitStatus.Open, problems: ClinicalNoteRules.MaxProblems + 1)));
        Assert.Throws<DomainRuleViolationException>(() => draft.Overwrite(Draft(VisitStatus.Open, new string('x', ClinicalNoteRules.MaxSoapFieldLength + 1))));
    }

    [Fact]
    public void TheDraftRaisesNoDomainEvent_ItIsNotTheRecord()
    {
        var draft = new ConsultationDraftAggregate(Guid.NewGuid());
        draft.Overwrite(Draft(VisitStatus.Open));

        Assert.Empty(draft.Events);
    }
}

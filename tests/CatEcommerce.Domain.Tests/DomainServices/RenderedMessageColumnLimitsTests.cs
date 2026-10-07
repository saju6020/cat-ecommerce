using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>
/// SECURITY/CODE_REVIEW gate fix (2026-09-27, second pass, MEDIUM). Pure -- no database. Reproduces
/// the shape of probe P6c at the unit level: a rendered field can overflow its
/// `notify.notification` column once substitution has run, even though the raw template (checked by
/// <c>TemplateRules</c>) was well within its own, per-channel limit.
/// </summary>
public sealed class RenderedMessageColumnLimitsTests
{
    private static RenderedMessage Message(string? subject = "Subject", string? title = "Title", string body = "Body", string? linkPath = "/x") =>
        new(subject, title, body, null, linkPath);

    [Fact]
    public void SubjectWithinLimit_IsUnchanged()
    {
        var rendered = Message(subject: new string('a', RenderedMessageColumnLimits.SubjectMaxLength));

        var enforced = RenderedMessageColumnLimits.Enforce(rendered);

        Assert.Equal(rendered.Subject, enforced.Subject);
    }

    [Fact]
    public void OverlongSubject_IsTruncatedWithEllipsis_AtExactlyTheColumnLimit()
    {
        var rendered = Message(subject: new string('a', RenderedMessageColumnLimits.SubjectMaxLength + 50));

        var enforced = RenderedMessageColumnLimits.Enforce(rendered);

        Assert.Equal(RenderedMessageColumnLimits.SubjectMaxLength, enforced.Subject!.Length);
        Assert.EndsWith("\u2026", enforced.Subject);
    }

    [Fact]
    public void OverlongTitle_IsTruncatedWithEllipsis_AtExactlyTheColumnLimit()
    {
        // Probe P6c's exact shape: a 76-character title template plus a 100-character cat name
        // pushes the rendered title well past its 100-character column.
        var rendered = Message(title: "Time for " + new string('M', 100) + "'s treatment - book now at the clinic");

        var enforced = RenderedMessageColumnLimits.Enforce(rendered);

        Assert.Equal(RenderedMessageColumnLimits.TitleMaxLength, enforced.Title!.Length);
        Assert.EndsWith("\u2026", enforced.Title);
    }

    [Fact]
    public void TruncationNeverSplitsASurrogatePair()
    {
        // CODE_REVIEW gate fix (2026-09-27, third pass, LOW): the original 99-'a' placement made
        // the naive cut point (cutAt = maxLength - Ellipsis.Length = 99) land exactly AFTER the 99
        // 'a's -- never touching the surrogate pair at all, so the back-off branch
        // (char.IsHighSurrogate(text[cutAt - 1])) never fired, and this test would still pass even
        // if that branch were deleted. 98 'a's, then the pair, then one more character makes
        // cutAt (99) land ON the high surrogate (text[98]), so the back-off actually executes.
        // U+1F431 CAT FACE is a surrogate pair in UTF-16.
        var cat = "\U0001F431"; // 2 UTF-16 code units
        var title = new string('a', RenderedMessageColumnLimits.TitleMaxLength - 2) + cat + "a";

        var enforced = RenderedMessageColumnLimits.Enforce(Message(title: title));

        Assert.True(enforced.Title!.Length <= RenderedMessageColumnLimits.TitleMaxLength);
        // No lone surrogate anywhere in the result.
        for (var i = 0; i < enforced.Title.Length; i++)
        {
            if (char.IsHighSurrogate(enforced.Title[i]))
                Assert.True(i + 1 < enforced.Title.Length && char.IsLowSurrogate(enforced.Title[i + 1]));
            if (char.IsLowSurrogate(enforced.Title[i]))
                Assert.True(i > 0 && char.IsHighSurrogate(enforced.Title[i - 1]));
        }
    }

    [Fact]
    public void OverlongBody_ThrowsNotificationRenderOverflowException_NeverTruncated()
    {
        var rendered = Message(body: new string('a', RenderedMessageColumnLimits.BodyMaxLength + 1));

        Assert.Throws<NotificationRenderOverflowException>(() => RenderedMessageColumnLimits.Enforce(rendered));
    }

    [Fact]
    public void OverlongBody_ExceptionIsAnInvalidOperationException_SoExistingClosedCatchSetsStillCatchIt()
    {
        var rendered = Message(body: new string('a', RenderedMessageColumnLimits.BodyMaxLength + 1));

        var ex = Record.Exception(() => RenderedMessageColumnLimits.Enforce(rendered));

        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    [Fact]
    public void OverlongLinkPath_ThrowsNotificationRenderOverflowException()
    {
        var rendered = Message(linkPath: "/" + new string('a', RenderedMessageColumnLimits.LinkPathMaxLength));

        Assert.Throws<NotificationRenderOverflowException>(() => RenderedMessageColumnLimits.Enforce(rendered));
    }

    [Fact]
    public void NullSubjectTitleAndLinkPath_AreLeftNull()
    {
        var rendered = new RenderedMessage(null, null, "Body", null, null);

        var enforced = RenderedMessageColumnLimits.Enforce(rendered);

        Assert.Null(enforced.Subject);
        Assert.Null(enforced.Title);
        Assert.Null(enforced.LinkPath);
    }
}

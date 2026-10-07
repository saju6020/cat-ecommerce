using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Thrown when a rendered field cannot be safely shortened to fit its persisted column (the body or
/// the link path). Treated exactly like any other poison template/value: a render failure
/// (<c>NotificationSuppressionReason.RenderFailed</c>), never a row stuck <c>Pending</c>/<c>Sending</c>
/// forever. Deliberately derives from <see cref="InvalidOperationException"/> so the existing closed
/// catch sets in <c>ExpandNotificationRequestCommandHandler</c>, <c>DeliverNotificationCommandHandler</c>
/// and <c>ResendNotificationCommandHandler</c> (which already catch
/// <see cref="InvalidOperationException"/> for the "missing resolved value" case) catch this without
/// needing a fourth exception type added to each of them.
/// </summary>
public sealed class NotificationRenderOverflowException(string message) : InvalidOperationException(message);

/// <summary>
/// HO-NTF-001 SECURITY/CODE_REVIEW gate fix (2026-09-27, second pass, MEDIUM). A rendered field
/// longer than its <c>notify.notification</c> column (see <c>NotificationConfiguration</c>) could
/// never be saved: Postgres raised 22001 (string data right truncation) in the final
/// <c>UpdateAsync</c>/<c>SaveAsync</c>, which sits OUTSIDE the render try/catch, so the row was stuck
/// -- <c>Pending</c> forever at C4 (retried every poll, oldest first, blocking every request behind
/// it including password resets), or re-claimed and re-sent every lease at C5's fallback path. This
/// is enforced HERE, before a row is ever built, not after Postgres rejects it:
/// <list type="bullet">
/// <item>Subject and Title are truncated with an ellipsis, since a shortened subject or title is
/// still meaningful and safe to send.</item>
/// <item>Body and LinkPath cannot be safely shortened without changing their meaning (a sentence
/// cut off mid-word, or a URL path truncated into a different, unintended route), so an overflow
/// there is a render failure.</item>
/// </list>
/// Template validation (<c>TemplateRules.ValidateLengths</c>) already bounds the RAW template before
/// substitution, but substitution can add up to <c>NotificationRequestRules.MaxVariableLength</c>
/// (2000) characters per placeholder, comfortably enough to overflow any of these columns.
/// </summary>
public static class RenderedMessageColumnLimits
{
    public const int SubjectMaxLength = 150;
    public const int TitleMaxLength = 100;
    public const int BodyMaxLength = 10_000;
    public const int LinkPathMaxLength = 500;

    private const string Ellipsis = "\u2026";

    /// <summary>Returns <paramref name="rendered"/> with Subject/Title truncated as needed, or
    /// throws <see cref="NotificationRenderOverflowException"/> if Body or LinkPath overflow.</summary>
    public static RenderedMessage Enforce(RenderedMessage rendered)
    {
        if (rendered.TextBody.Length > BodyMaxLength)
            throw new NotificationRenderOverflowException(
                $"Rendered body is {rendered.TextBody.Length} characters, over the {BodyMaxLength}-character column limit.");

        if (rendered.LinkPath is { } linkPath && linkPath.Length > LinkPathMaxLength)
            throw new NotificationRenderOverflowException(
                $"Rendered link path is {linkPath.Length} characters, over the {LinkPathMaxLength}-character column limit.");

        return rendered with
        {
            Subject = Truncate(rendered.Subject, SubjectMaxLength),
            Title = Truncate(rendered.Title, TitleMaxLength)
        };
    }

    /// <summary>Never splits a UTF-16 surrogate pair: backs the cut point off by one more character
    /// when it would otherwise land between a high and a low surrogate.</summary>
    private static string? Truncate(string? text, int maxLength)
    {
        if (text is null || text.Length <= maxLength) return text;

        var cutAt = maxLength - Ellipsis.Length;
        if (cutAt > 0 && char.IsHighSurrogate(text[cutAt - 1])) cutAt--;
        if (cutAt < 0) cutAt = 0;

        return text[..cutAt] + Ellipsis;
    }
}

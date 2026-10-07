using System.Globalization;
using System.Text.RegularExpressions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>One violation from <see cref="TemplateRules.Validate"/> (HO-NTF-001 §8.2).</summary>
public sealed record TemplateViolation(string Code, string Message, string PropertyName);

/// <summary>
/// HO-NTF-001 §8.2 -- pure validation of a template definition's contents against the catalogue
/// definition. Returns the first violation found, or <c>null</c> when valid (matches
/// <c>LimitedMarkup.Validate</c>'s single-code shape elsewhere in this codebase).
/// </summary>
public static partial class TemplateRules
{
    private static readonly Regex ValidPlaceholder = ValidPlaceholderRegex();
    private static readonly Regex DisallowedPlainText = DisallowedPlainTextRegex();

    public static TemplateViolation? Validate(NotificationTemplateDefinition definition, IReadOnlyList<TemplateContentInput> contents)
    {
        var isMarketing = definition.Category == NotificationCategory.Marketing;

        // Rule 1: exactly the definition's channels, no other channel.
        var contentChannels = contents.Select(c => c.Channel).ToList();
        if (contentChannels.Count != definition.Channels.Count ||
            !definition.Channels.All(c => contentChannels.Contains(c)) ||
            contentChannels.Distinct().Count() != contentChannels.Count)
        {
            return new TemplateViolation(BusinessViolationCodes.TemplateChannelMismatch,
                "The template must have exactly one content block per channel the definition declares.", "Contents");
        }

        for (var i = 0; i < contents.Count; i++)
        {
            var content = contents[i];
            var prefix = $"Contents[{i}]";

            foreach (var (fieldName, text) in RelevantFields(content))
            {
                if (text is null) continue;

                if (ControlCharacters.ContainsPrivateUseOrFormat(text))
                    return new TemplateViolation(BusinessViolationCodes.InvalidTemplateText,
                        "Contains a disallowed private-use or format character.", $"{prefix}.{fieldName}");

                var malformed = FindMalformedPlaceholder(text);
                if (malformed is not null)
                    return new TemplateViolation(BusinessViolationCodes.MalformedTemplatePlaceholder,
                        $"'{malformed}' is not a well-formed placeholder.", $"{prefix}.{fieldName}");

                foreach (Match match in ValidPlaceholder.Matches(text))
                {
                    var name = match.Groups[1].Value;
                    if (!IsKnownVariable(name, definition, isMarketing))
                        return new TemplateViolation(BusinessViolationCodes.UnknownTemplatePlaceholder,
                            $"{{{{{name}}}}} is not a variable of this template. Available: {AvailableVariableList(definition, isMarketing)}.",
                            $"{prefix}.{fieldName}");
                }
            }

            // Rule 4: required-on variables (definition's own + unsubscribeLink for marketing).
            var requiredNames = definition.Variables
                .Where(v => v.RequiredOn.Contains(content.Channel))
                .Select(v => v.Name)
                .ToList();
            if (isMarketing) requiredNames.Add(StandardTemplateVariables.UnsubscribeLink);

            // Deviation from §8.2 rule 4's literal "missing from that channel's body" wording: an
            // in-app notification's actionable link naturally lives in LinkPath, not repeated as
            // inline body text (the shipped care.vaccination-due/care.deworming-due InApp content
            // does exactly this) -- so a required Url variable also counts as present if it is the
            // channel's LinkPath. Any other required variable is still checked against the body only.
            foreach (var required in requiredNames)
            {
                var presentInBody = ContainsPlaceholder(content.Body, required);
                var presentAsLinkPath = ContainsPlaceholder(content.LinkPath, required);

                if (!presentInBody && !presentAsLinkPath)
                    return new TemplateViolation(BusinessViolationCodes.RequiredTemplatePlaceholderMissing,
                        $"{{{{{required}}}}} must appear in the body (or, for InApp, the link) for this channel.", $"{prefix}.Body");
            }

            var lengthViolation = ValidateLengths(content, prefix);
            if (lengthViolation is not null) return lengthViolation;

            if (content.Channel == NotificationChannel.Email)
            {
                // TemplateRules' own extension over LimitedMarkup: a link target may also be a
                // single Url-kind placeholder (§8.2 rule 6). Checked BEFORE LimitedMarkup's own
                // https/site-relative-only rule, on a copy with placeholder link targets swapped
                // for a dummy https URL, so a shipped template's {{resetLink}}/{{bookingLink}}
                // link does not spuriously fail LimitedMarkup's own check.
                var linkError = ValidateEmailLinkTargets(content.Body, definition);
                if (linkError is not null) return new TemplateViolation(BusinessViolationCodes.InvalidTemplateLink, linkError, $"{prefix}.Body");

                var markupError = LimitedMarkup.Validate(NeutralizePlaceholderLinks(content.Body));
                if (markupError is not null)
                    return new TemplateViolation(
                        markupError == BusinessViolationCodes.InvalidContentLink ? BusinessViolationCodes.InvalidTemplateLink : BusinessViolationCodes.InvalidTemplateText,
                        "Email body failed markup validation.", $"{prefix}.Body");
            }
            else
            {
                if (DisallowedPlainText.IsMatch(content.Body ?? ""))
                    return new TemplateViolation(BusinessViolationCodes.InvalidTemplateText,
                        "This channel's body must be plain text -- no markup or link syntax.", $"{prefix}.Body");
            }

            if (content.Channel == NotificationChannel.InApp)
            {
                var linkPathError = ValidateInAppLinkPath(content.LinkPath, definition);
                if (linkPathError is not null) return new TemplateViolation(BusinessViolationCodes.InvalidTemplateLink, linkPathError, $"{prefix}.LinkPath");
            }
        }

        return null;
    }

    /// <summary>Maximum length for a caller-supplied preview sample override -- SECURITY gate LOW
    /// ("preview also accepts a 2 MB sampleValues entry and echoes it back").</summary>
    public const int MaxSampleValueLength = 500;

    /// <summary>
    /// HO-NTF-001 SECURITY/CODE_REVIEW gate fix (2026-09-27, LOW). Validates a caller-supplied
    /// preview sample override against the same constraints <see cref="NotificationRenderer"/>
    /// silently assumes are already true (a <c>Url</c> kind must be an absolute https URL; a
    /// <c>Date</c> kind must be <c>yyyy-MM-dd</c>), so a malformed override is refused with <c>400</c>
    /// before it ever reaches the renderer, instead of throwing <c>InvalidOperationException</c> or
    /// <c>FormatException</c> there.
    /// </summary>
    public static TemplateViolation? ValidateSampleOverride(string variableName, TemplateVariableKind kind, string value, string propertyName)
    {
        if (value.Length > MaxSampleValueLength)
            return new TemplateViolation(BusinessViolationCodes.InvalidTemplateSampleValue,
                $"'{variableName}' sample value cannot exceed {MaxSampleValueLength} characters.", propertyName);

        if (ControlCharacters.ContainsDisallowed(value) || ControlCharacters.ContainsPrivateUseOrFormat(value))
            return new TemplateViolation(BusinessViolationCodes.InvalidTemplateSampleValue,
                $"'{variableName}' sample value contains a disallowed character.", propertyName);

        if (kind == TemplateVariableKind.Url && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return new TemplateViolation(BusinessViolationCodes.InvalidTemplateSampleValue,
                $"'{variableName}' is a Url variable and its sample value must be an absolute https URL.", propertyName);

        if (kind == TemplateVariableKind.Date && !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return new TemplateViolation(BusinessViolationCodes.InvalidTemplateSampleValue,
                $"'{variableName}' is a Date variable and its sample value must be yyyy-MM-dd.", propertyName);

        return null;
    }

    private static IEnumerable<(string Field, string? Text)> RelevantFields(TemplateContentInput content)
    {
        yield return ("Subject", content.Subject);
        yield return ("Title", content.Title);
        yield return ("Body", content.Body);
        yield return ("LinkPath", content.LinkPath);
    }

    private static bool IsKnownVariable(string name, NotificationTemplateDefinition definition, bool isMarketing) =>
        definition.Variables.Any(v => v.Name == name) || StandardTemplateVariables.IsStandard(name, isMarketing);

    private static string AvailableVariableList(NotificationTemplateDefinition definition, bool isMarketing)
    {
        var names = definition.Variables.Select(v => v.Name).Concat(StandardTemplateVariables.Common);
        if (isMarketing) names = names.Append(StandardTemplateVariables.UnsubscribeLink);
        return string.Join(", ", names);
    }

    private static bool ContainsPlaceholder(string? text, string name) =>
        text is not null && ValidPlaceholder.Matches(text).Any(m => m.Groups[1].Value == name);

    /// <summary>Finds any <c>{{</c>/<c>}}</c>/<c>{</c>/<c>}</c> left over once every well-formed
    /// placeholder is removed -- catches spaced (<c>{{ x }}</c>), single-braced (<c>{x}</c>) and
    /// unterminated forms alike (U-A2).</summary>
    private static string? FindMalformedPlaceholder(string text)
    {
        var remaining = ValidPlaceholder.Replace(text, "");
        var index = remaining.IndexOfAny(['{', '}']);
        if (index < 0) return null;

        var length = Math.Min(20, remaining.Length - index);
        return remaining.Substring(index, length);
    }

    private static TemplateViolation? ValidateLengths(TemplateContentInput content, string prefix)
    {
        if (content.Channel == NotificationChannel.Email)
        {
            if (string.IsNullOrEmpty(content.Subject) || content.Subject.Length > 150)
                return new TemplateViolation(BusinessViolationCodes.InvalidTemplateLength, "Email subject must be 1-150 characters.", $"{prefix}.Subject");
            if (string.IsNullOrEmpty(content.Body) || content.Body.Length > 10_000)
                return new TemplateViolation(BusinessViolationCodes.InvalidTemplateLength, "Email body must be 1-10000 characters.", $"{prefix}.Body");
        }
        else if (content.Channel == NotificationChannel.Sms)
        {
            if (string.IsNullOrEmpty(content.Body) || content.Body.Length > 480)
                return new TemplateViolation(BusinessViolationCodes.InvalidTemplateLength, "SMS body must be 1-480 characters.", $"{prefix}.Body");
        }
        else if (content.Channel == NotificationChannel.InApp)
        {
            if (string.IsNullOrEmpty(content.Title) || content.Title.Length > 100)
                return new TemplateViolation(BusinessViolationCodes.InvalidTemplateLength, "In-app title must be 1-100 characters.", $"{prefix}.Title");
            if (string.IsNullOrEmpty(content.Body) || content.Body.Length > 1000)
                return new TemplateViolation(BusinessViolationCodes.InvalidTemplateLength, "In-app body must be 1-1000 characters.", $"{prefix}.Body");
        }
        else if (content.Channel == NotificationChannel.Push)
        {
            if (string.IsNullOrEmpty(content.Title) || content.Title.Length > 65)
                return new TemplateViolation(BusinessViolationCodes.InvalidTemplateLength, "Push title must be 1-65 characters.", $"{prefix}.Title");
            if (string.IsNullOrEmpty(content.Body) || content.Body.Length > 240)
                return new TemplateViolation(BusinessViolationCodes.InvalidTemplateLength, "Push body must be 1-240 characters.", $"{prefix}.Body");
        }

        return null;
    }

    /// <summary>Rule 6's extension: a link target may be an https URL, a site-relative path, or a
    /// single <c>Url</c>-kind placeholder -- never a <c>Text</c>/<c>Date</c> variable.</summary>
    private static string? ValidateEmailLinkTargets(string body, NotificationTemplateDefinition definition)
    {
        foreach (Match match in MarkupLinkRegex().Matches(body))
        {
            var url = match.Groups["url"].Value;
            var placeholderMatch = ValidPlaceholder.Match(url);
            if (placeholderMatch.Success && placeholderMatch.Value == url)
            {
                var name = placeholderMatch.Groups[1].Value;
                var isUrlKind = StandardTemplateVariables.Kinds.TryGetValue(name, out var stdKind) && stdKind == TemplateVariableKind.Url
                    || definition.Variables.Any(v => v.Name == name && v.Kind == TemplateVariableKind.Url);
                if (!isUrlKind) return $"'{{{{{name}}}}}' is not a Url variable and cannot be used as a link target.";
                continue;
            }

            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !IsSiteRelativePath(url))
                return "A link target must be https, site-relative, or a single Url placeholder.";
        }

        return null;
    }

    /// <summary>Replaces a bare-placeholder link target (already checked valid by
    /// <see cref="ValidateEmailLinkTargets"/>) with a dummy https URL, so <c>LimitedMarkup.Validate</c>
    /// -- which knows nothing about placeholders -- does not reject it.</summary>
    private static string NeutralizePlaceholderLinks(string body) =>
        MarkupLinkRegex().Replace(body, m =>
        {
            var url = m.Groups["url"].Value;
            var placeholderMatch = ValidPlaceholder.Match(url);
            if (!placeholderMatch.Success || placeholderMatch.Value != url) return m.Value;

            var urlStart = m.Groups["url"].Index - m.Index;
            return m.Value[..urlStart] + "https://placeholder.example" + m.Value[(urlStart + url.Length)..];
        });

    private static string? ValidateInAppLinkPath(string? linkPath, NotificationTemplateDefinition definition)
    {
        if (string.IsNullOrEmpty(linkPath)) return null;
        if (IsSiteRelativePath(linkPath)) return null;

        var placeholderMatch = ValidPlaceholder.Match(linkPath);
        if (placeholderMatch.Success && placeholderMatch.Value == linkPath)
        {
            var name = placeholderMatch.Groups[1].Value;
            var isUrlKind = definition.Variables.Any(v => v.Name == name && v.Kind == TemplateVariableKind.Url);
            if (isUrlKind) return null;
        }

        return "LinkPath must be empty, a site-relative path, or a single Url placeholder.";
    }

    /// <summary>
    /// HO-NTF-001 SECURITY gate fix (2026-09-27, second pass, LOW). "Site-relative" used to accept
    /// any string starting with a single <c>/</c>, including <c>/\evil.test</c> -- WHATWG URL parsing
    /// treats a backslash as a forward slash for http(s) schemes, so a browser resolves that to
    /// <c>https://evil.test/</c>, a fully attacker-controlled cross-origin link. A genuine
    /// site-relative path never needs a backslash, so any backslash anywhere in the target -- not
    /// just a leading <c>/\</c> or <c>\\</c> -- now disqualifies it.
    /// </summary>
    private static bool IsSiteRelativePath(string url) =>
        url.StartsWith('/')
        && !url.StartsWith("//", StringComparison.Ordinal)
        && !url.Contains('\\');

    [GeneratedRegex(@"\{\{([a-z][A-Za-z0-9]*)\}\}")]
    private static partial Regex ValidPlaceholderRegex();

    [GeneratedRegex(@"<|\[[^\]]*\]\([^)]*\)")]
    private static partial Regex DisallowedPlainTextRegex();

    [GeneratedRegex(@"\[[^\]]*\]\((?<url>[^)]*)\)")]
    private static partial Regex MarkupLinkRegex();
}

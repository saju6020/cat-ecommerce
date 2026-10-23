using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-NTF-001 §8.3 / ADR-062 -- pure, no I/O. <b>Parses the template first, substitutes values
/// second</b>: every <c>{{name}}</c> occurrence in the raw, admin-authored template is replaced by
/// an opaque, non-printable sentinel token before any markdown-like structure (headings, bullets,
/// bold, links) is recognised, and the real value is spliced back in -- HTML-encoded for
/// <see cref="RenderedMessage.HtmlBody"/> -- only after every tag has already been decided. A value
/// can therefore never introduce new markup or a new placeholder: <c>Regex.Replace</c> matches
/// against the ORIGINAL string once, so a value containing literal <c>{{x}}</c> text is never
/// re-expanded (U-A9), and a value containing <c>**</c>/<c>[...](...)</c> sequences can never be
/// mistaken for template-authored markdown because those characters are only ever recognised in the
/// sentinel-bearing (i.e. not-yet-substituted) copy.
/// </summary>
public static partial class NotificationRenderer
{
    private const char SentinelStart = '';
    private const char SentinelEnd = '';

    public static RenderedMessage Render(
        NotificationTemplateChannelDefault content,
        IReadOnlyList<NotificationTemplateVariableDefinition> variables,
        IReadOnlyDictionary<string, string> values,
        NotificationChannel channel)
    {
        var kindByName = variables.ToDictionary(v => v.Name, v => v.Kind);
        foreach (var (name, kind) in StandardTemplateVariables.Kinds) kindByName.TryAdd(name, kind);

        var resolved = new List<string>();

        string Substitute(string? text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            return ValidPlaceholderRegex().Replace(text, m =>
            {
                var name = m.Groups[1].Value;
                if (!values.TryGetValue(name, out var raw))
                    throw new InvalidOperationException($"No resolved value supplied for '{{{{{name}}}}}'.");

                var kind = kindByName.GetValueOrDefault(name, TemplateVariableKind.Text);
                resolved.Add(ResolveDisplay(raw, kind, name));
                return Sentinel(resolved.Count - 1);
            });
        }

        var bodyWithSentinels = Substitute(content.Body);
        var subjectWithSentinels = Substitute(content.Subject);
        var titleWithSentinels = Substitute(content.Title);
        var linkPathWithSentinels = Substitute(content.LinkPath);

        if (channel == NotificationChannel.Email)
        {
            var htmlBody = RenderEmailHtml(bodyWithSentinels, resolved, values);
            var textBody = Desentinel(StripMarkupForPlainText(bodyWithSentinels), resolved, encode: false);
            var subject = Desentinel(subjectWithSentinels, resolved, encode: false);
            return new RenderedMessage(subject, null, textBody, htmlBody, null);
        }

        var plainBody = Desentinel(bodyWithSentinels, resolved, encode: false);
        var plainTitle = string.IsNullOrEmpty(titleWithSentinels) ? null : Desentinel(titleWithSentinels, resolved, encode: false);
        var linkPath = string.IsNullOrEmpty(linkPathWithSentinels) ? null : Desentinel(linkPathWithSentinels, resolved, encode: false);

        return new RenderedMessage(null, plainTitle, plainBody, null, linkPath);
    }

    private static string ResolveDisplay(string raw, TemplateVariableKind kind, string name)
    {
        if (kind == TemplateVariableKind.Url)
        {
            return raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || IsLocalDevelopmentLink(raw)
                ? raw
                : throw new InvalidOperationException($"'{name}' is a Url variable and must be resolved to an absolute https URL before rendering.");
        }

        if (kind == TemplateVariableKind.Date)
            return DateOnly.ParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("d MMM yyyy", CultureInfo.InvariantCulture);

        return raw;
    }

    /// <summary>Owner decision 2026-10-15: a plain-http link to this machine (<c>http://localhost</c>, <c>http://127.0.0.1</c>)
    /// is allowed so password reset works on a developer's machine. It is reachable only in Development/Testing: every link
    /// is built from <c>PublicSite:WebBaseUrl</c>, which startup refuses unless it is https outside those environments
    /// (CatProfilesModule). Any other http link is still refused.</summary>
    private static bool IsLocalDevelopmentLink(string raw) =>
        Uri.TryCreate(raw, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp
        && string.IsNullOrEmpty(uri.UserInfo)
        && (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) || uri.Host == "127.0.0.1");

    private static string Sentinel(int index) => $"{SentinelStart}{index}{SentinelEnd}";

    private static string Desentinel(string text, IReadOnlyList<string> values, bool encode) =>
        SentinelPatternRegex().Replace(text, m =>
        {
            var value = values[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)];
            return encode ? WebUtility.HtmlEncode(value) : value;
        });

    /// <summary>Best-effort plain-text alternative: strips the block/inline markup tokens (not the
    /// sentinel placeholders) so the text-only alternative reads naturally.</summary>
    private static string StripMarkupForPlainText(string markupWithSentinels)
    {
        var lines = markupWithSentinels.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            var stripped = line;
            if (stripped.StartsWith("### ")) stripped = stripped[4..];
            else if (stripped.StartsWith("## ")) stripped = stripped[3..];
            else if (stripped.StartsWith("- ")) stripped = "- " + stripped[2..];

            stripped = BoldPatternRegex().Replace(stripped, "$1");
            stripped = LinkPatternRegex().Replace(stripped, "$1 ($2)");
            sb.AppendLine(stripped);
        }

        return sb.ToString().TrimEnd();
    }

    private static string RenderEmailHtml(string markupWithSentinels, IReadOnlyList<string> resolved, IReadOnlyDictionary<string, string> values)
    {
        var lines = markupWithSentinels.Replace("\r\n", "\n").Split('\n');
        var body = new StringBuilder();
        var bulletBuffer = new List<string>();

        void FlushBullets()
        {
            if (bulletBuffer.Count == 0) return;
            body.Append("<ul>");
            foreach (var item in bulletBuffer) body.Append("<li>").Append(RenderInline(item)).Append("</li>");
            body.Append("</ul>");
            bulletBuffer.Clear();
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("### ")) { FlushBullets(); body.Append("<h3>").Append(RenderInline(line[4..])).Append("</h3>"); }
            else if (line.StartsWith("## ")) { FlushBullets(); body.Append("<h2>").Append(RenderInline(line[3..])).Append("</h2>"); }
            else if (line.StartsWith("- ")) bulletBuffer.Add(line[2..]);
            else if (line.Trim().Length == 0) FlushBullets();
            else { FlushBullets(); body.Append("<p>").Append(RenderInline(line)).Append("</p>"); }
        }

        FlushBullets();

        var clinicName = Desentinel(values.GetValueOrDefault(StandardTemplateVariables.ClinicName, ""), resolved, encode: true);
        var clinicPhone = Desentinel(values.GetValueOrDefault(StandardTemplateVariables.ClinicPhone, ""), resolved, encode: true);
        var unsubscribeLink = values.TryGetValue(StandardTemplateVariables.UnsubscribeLink, out var link) ? link : null;

        var shell = new StringBuilder();
        shell.Append("<div style=\"font-family:sans-serif;max-width:600px;margin:0 auto;\">");
        shell.Append("<div style=\"padding:16px 0;border-bottom:1px solid #eee;font-weight:bold;\">").Append(WebUtility.HtmlEncode(clinicName)).Append("</div>");
        shell.Append("<div style=\"padding:16px 0;\">").Append(Desentinel(body.ToString(), resolved, encode: true)).Append("</div>");
        shell.Append("<div style=\"padding:16px 0;border-top:1px solid #eee;font-size:12px;color:#666;\">");
        shell.Append(WebUtility.HtmlEncode(clinicPhone));
        if (!string.IsNullOrEmpty(unsubscribeLink))
            shell.Append(" &middot; <a href=\"").Append(WebUtility.HtmlEncode(unsubscribeLink)).Append("\">Unsubscribe</a>");
        shell.Append("</div></div>");

        return shell.ToString();
    }

    private static string RenderInline(string line)
    {
        var encoded = WebUtility.HtmlEncode(line);
        encoded = LinkPatternRegex().Replace(encoded, m => $"<a href=\"{m.Groups[2].Value}\">{RenderBold(m.Groups[1].Value)}</a>");
        return RenderBold(encoded);
    }

    private static string RenderBold(string text) => BoldPatternRegex().Replace(text, "<strong>$1</strong>");

    [GeneratedRegex(@"\{\{([a-z][A-Za-z0-9]*)\}\}")]
    private static partial Regex ValidPlaceholderRegex();

    [GeneratedRegex("(\\d+)")]
    private static partial Regex SentinelPatternRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldPatternRegex();

    [GeneratedRegex(@"\[([^\]]*)\]\(([^)]*)\)")]
    private static partial Regex LinkPatternRegex();
}

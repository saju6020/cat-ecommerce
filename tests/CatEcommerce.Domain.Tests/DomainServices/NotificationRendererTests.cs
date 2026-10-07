using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-NTF-001 §15 U-A9..U-A11. Pure -- no database.</summary>
public sealed class NotificationRendererTests
{
    private static readonly IReadOnlyList<NotificationTemplateVariableDefinition> Variables =
    [
        new NotificationTemplateVariableDefinition("catName", "d", TemplateVariableKind.Text, "Mitu", []),
        new NotificationTemplateVariableDefinition("vaccineName", "d", TemplateVariableKind.Text, "Rabies", []),
        new NotificationTemplateVariableDefinition("dueOn", "d", TemplateVariableKind.Date, "2026-10-12", []),
        new NotificationTemplateVariableDefinition("bookingLink", "d", TemplateVariableKind.Url, "https://x/y", []),
    ];

    private static Dictionary<string, string> BaseValues() => new()
    {
        ["catName"] = "Mitu",
        ["vaccineName"] = "Rabies",
        ["dueOn"] = "2026-10-12",
        ["bookingLink"] = "https://portal.example.com/portal/cats/abc",
        [StandardTemplateVariables.ClinicName] = "PurrCare",
        [StandardTemplateVariables.ClinicPhone] = "+8801700000000",
    };

    [Fact]
    public void MaliciousCatName_RendersAsLiteralText_NoMarkupNoExpansion()
    {
        var values = BaseValues();
        values["catName"] = "**Mitu**](https://evil){{resetLink}}";

        var content = new NotificationTemplateChannelDefault(NotificationChannel.Email, "Subject", null, "Hello {{catName}}!", null);
        var rendered = NotificationRenderer.Render(content, Variables, values, NotificationChannel.Email);

        Assert.DoesNotContain("<strong>", rendered.HtmlBody);
        Assert.DoesNotContain("<a ", rendered.HtmlBody);
        Assert.Contains("**Mitu**]", rendered.TextBody);
        Assert.Contains("(https://evil)", rendered.TextBody);
        // The literal characters appear HTML-encoded, and {{resetLink}} was never expanded --
        // no resetLink value exists in `values`, so an expansion attempt would have thrown.
        Assert.Contains("Mitu", rendered.HtmlBody!);
    }

    [Fact]
    public void ScriptTagInValue_IsHtmlEncodedInHtmlBody_LiteralInTextBody()
    {
        var values = BaseValues();
        values["catName"] = "<script>";

        var content = new NotificationTemplateChannelDefault(NotificationChannel.Email, "Subject", null, "Hello {{catName}}!", null);
        var rendered = NotificationRenderer.Render(content, Variables, values, NotificationChannel.Email);

        Assert.Contains("&lt;script&gt;", rendered.HtmlBody);
        Assert.Contains("<script>", rendered.TextBody);
    }

    [Fact]
    public void DateVariable_RendersInvariantEnglishFormat()
    {
        var values = BaseValues();
        var content = new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null, "Due {{dueOn}}", null);
        var rendered = NotificationRenderer.Render(content, Variables, values, NotificationChannel.Sms);

        Assert.Equal("Due 12 Oct 2026", rendered.TextBody);
    }

    [Fact]
    public void UrlVariable_NotHttps_Throws()
    {
        var values = BaseValues();
        values["bookingLink"] = "http://not-https.example.com";

        var content = new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null, "Link {{bookingLink}}", null);

        Assert.Throws<InvalidOperationException>(() => NotificationRenderer.Render(content, Variables, values, NotificationChannel.Sms));
    }

    [Fact]
    public void EmailLink_WithPlaceholderTarget_RendersAnchorTag()
    {
        var values = BaseValues();
        var content = new NotificationTemplateChannelDefault(NotificationChannel.Email, "Subject", null, "[Book now]({{bookingLink}})", null);
        var rendered = NotificationRenderer.Render(content, Variables, values, NotificationChannel.Email);

        Assert.Contains($"<a href=\"{values["bookingLink"]}\">Book now</a>", rendered.HtmlBody);
    }
}

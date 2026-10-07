using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-NTF-001 §15 U-A1..U-A8. Pure -- no database.</summary>
public sealed class TemplateRulesTests
{
    private static readonly NotificationTemplateDefinition VaccinationDefinition =
        NotificationTemplateCatalogue.Find(NotificationTemplateCatalogue.VaccinationDue)!;

    private static readonly NotificationTemplateDefinition ResetDefinition =
        NotificationTemplateCatalogue.Find(NotificationTemplateCatalogue.PasswordReset)!;

    private static TemplateContentInput Sms(string body) => new(NotificationChannel.Sms, null, null, body, null);
    private static TemplateContentInput Email(string body, string subject = "Subject") => new(NotificationChannel.Email, subject, null, body, null);
    private static TemplateContentInput InApp(string body, string title = "Title", string? linkPath = null) => new(NotificationChannel.InApp, null, title, body, linkPath);

    private static IReadOnlyList<TemplateContentInput> FullVaccinationContents(TemplateContentInput? overrideSms = null, TemplateContentInput? overrideEmail = null, TemplateContentInput? overrideInApp = null) =>
    [
        overrideSms ?? Sms("{{catName}}'s {{vaccineName}} is due {{dueOn}} {{bookingLink}}"),
        overrideEmail ?? Email("{{catName}}'s {{vaccineName}} is due {{dueOn}}. [Book]({{bookingLink}})"),
        overrideInApp ?? InApp("{{catName}}'s {{vaccineName}} is due {{dueOn}}", linkPath: "{{bookingLink}}")
    ];

    [Fact]
    public void UnknownPlaceholder_NamesTheVariableAndPropertyPath()
    {
        var contents = FullVaccinationContents(overrideSms: Sms("{{catname}} is due"));

        var violation = TemplateRules.Validate(VaccinationDefinition, contents);

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.UnknownTemplatePlaceholder, violation!.Code);
        Assert.Contains("{{catname}}", violation.Message);
        Assert.StartsWith("Contents[0].Body", violation.PropertyName);
    }

    [Theory]
    [InlineData("{{ catName }} is due")]
    [InlineData("{catName} is due")]
    [InlineData("{{catName is due")]
    [InlineData("}} alone")]
    public void MalformedPlaceholder_IsRejected(string body)
    {
        var contents = FullVaccinationContents(overrideSms: Sms(body));
        var violation = TemplateRules.Validate(VaccinationDefinition, contents);

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.MalformedTemplatePlaceholder, violation!.Code);
    }

    [Fact]
    public void MissingRequiredPlaceholder_IsRejected()
    {
        var contents = FullVaccinationContents(overrideSms: Sms("{{catName}}'s vaccine is due {{dueOn}}"));
        var violation = TemplateRules.Validate(VaccinationDefinition, contents);

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.RequiredTemplatePlaceholderMissing, violation!.Code);
    }

    [Fact]
    public void MarketingTemplateWithoutUnsubscribeLink_IsRejected_TransactionalWithIt_IsUnknown()
    {
        var marketingDefinition = new NotificationTemplateDefinition(
            "shop.back-in-stock", "Back in stock", "d", NotificationCategory.Marketing, NotificationUrgency.Normal,
            [NotificationChannel.Email], false,
            [new NotificationTemplateVariableDefinition("productName", "d", TemplateVariableKind.Text, "Food", [])],
            [new NotificationTemplateChannelDefault(NotificationChannel.Email, "Back in stock", null, "{{productName}} is back!", null)]);

        var withoutUnsubscribe = TemplateRules.Validate(marketingDefinition, [Email("{{productName}} is back!")]);
        Assert.Equal(BusinessViolationCodes.RequiredTemplatePlaceholderMissing, withoutUnsubscribe!.Code);

        var transactionalWithUnsubscribe = TemplateRules.Validate(ResetDefinition,
        [
            Sms("Reset: {{resetLink}} exp {{expiresInMinutes}}"),
            Email("Reset: [link]({{resetLink}}) {{unsubscribeLink}}")
        ]);
        Assert.Equal(BusinessViolationCodes.UnknownTemplatePlaceholder, transactionalWithUnsubscribe!.Code);
    }

    [Fact]
    public void ChannelSetMismatch_MissingOne_IsRejected()
    {
        var contents = new[] { Sms("{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}"), Email("Body {{bookingLink}}") };
        var violation = TemplateRules.Validate(VaccinationDefinition, contents);

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.TemplateChannelMismatch, violation!.Code);
    }

    [Fact]
    public void ChannelSetMismatch_ExtraChannel_IsRejected()
    {
        var contents = FullVaccinationContents().Append(new TemplateContentInput(NotificationChannel.Push, null, "T", "body", null)).ToList();
        var violation = TemplateRules.Validate(VaccinationDefinition, contents);

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.TemplateChannelMismatch, violation!.Code);
    }

    [Fact]
    public void EmailBodyWithHtmlTag_IsRejected()
    {
        var contents = FullVaccinationContents(overrideEmail: Email("<b>{{catName}}</b> {{vaccineName}} {{dueOn}} {{bookingLink}}"));
        var violation = TemplateRules.Validate(VaccinationDefinition, contents);

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.InvalidTemplateText, violation!.Code);
    }

    [Fact]
    public void EmailLinkTarget_TextVariable_IsRejected()
    {
        var contents = FullVaccinationContents(overrideEmail: Email("{{dueOn}} {{bookingLink}} [x]({{catName}})"));
        var violation = TemplateRules.Validate(VaccinationDefinition, contents);

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.InvalidTemplateLink, violation!.Code);
    }

    [Fact]
    public void SmsBodyWithAngleBracketOrLinkSyntax_IsRejected()
    {
        var withAngle = TemplateRules.Validate(VaccinationDefinition, FullVaccinationContents(overrideSms: Sms("<{{catName}}> {{vaccineName}} {{dueOn}} {{bookingLink}}")));
        Assert.Equal(BusinessViolationCodes.InvalidTemplateText, withAngle!.Code);

        var withLink = TemplateRules.Validate(VaccinationDefinition, FullVaccinationContents(overrideSms: Sms("[x](https://a) {{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}")));
        Assert.Equal(BusinessViolationCodes.InvalidTemplateText, withLink!.Code);
    }

    [Fact]
    public void EveryShippedDefinition_DefaultsPassValidation_AndKeysAreUniqueAndWellFormed()
    {
        var keyPattern = new System.Text.RegularExpressions.Regex(@"^[a-z]+(\.[a-z0-9-]+)+$");

        foreach (var definition in NotificationTemplateCatalogue.Definitions)
        {
            Assert.True(definition.Key.Length <= 80);
            Assert.Matches(keyPattern, definition.Key);

            var contents = definition.Channels
                .Select(channel => definition.DefaultFor(channel))
                .Select(d => new TemplateContentInput(d!.Channel, d.Subject, d.Title, d.Body, d.LinkPath))
                .ToList();

            var violation = TemplateRules.Validate(definition, contents);
            Assert.Null(violation);
        }

        Assert.Equal(NotificationTemplateCatalogue.Definitions.Count, NotificationTemplateCatalogue.Definitions.Select(d => d.Key).Distinct().Count());
    }

    /// <summary>HO-NTF-001 SECURITY/CODE_REVIEW gate fix (2026-09-27, MEDIUM 7). Reproduces the
    /// production bug directly: before this rule existed, this exact body saved with <c>204</c>
    /// and then threw <c>ArgumentOutOfRangeException</c> on every later render (SECURITY probe P6).</summary>
    [Fact]
    public void BodyContainingSentinelCharacter_IsRejected()
    {
        var contents = FullVaccinationContents(overrideInApp: InApp("{{catName}} 99 due {{dueOn}}", linkPath: "{{bookingLink}}"));

        var violation = TemplateRules.Validate(VaccinationDefinition, contents);

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.InvalidTemplateText, violation!.Code);
        Assert.StartsWith("Contents[2].Body", violation.PropertyName);
    }

    [Fact]
    public void ZeroWidthSpaceInLinkPath_IsRejected()
    {
        var contents = FullVaccinationContents(overrideInApp: InApp("{{catName}}'s {{vaccineName}} is due {{dueOn}}", linkPath: "/portal​/cats"));

        var violation = TemplateRules.Validate(VaccinationDefinition, contents);

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.InvalidTemplateText, violation!.Code);
        Assert.StartsWith("Contents[2].LinkPath", violation.PropertyName);
    }

    /// <summary>
    /// SECURITY gate fix (2026-09-27, second pass, LOW). WHATWG URL parsing treats a backslash as a
    /// forward slash for http(s) schemes, so a browser resolves <c>/\evil.test</c> to
    /// <c>https://evil.test/</c> -- a fully attacker-controlled cross-origin link -- even though the
    /// old "site-relative" check (<c>StartsWith('/') &amp;&amp; !StartsWith("//")</c>) accepted it.
    /// This is a verbatim string literal (<c>@"..."</c>): the pass-1 probe used a normal C# string
    /// literal, where <c>"/\e..."</c> compiles to <c>/</c> + the escape sequence <c>\e</c> (an ESC
    /// control character), which the UNRELATED control-character rule rejected for the wrong reason
    /// and the escaping bug then hid the real gap. A verbatim literal keeps the backslash literal.
    /// </summary>
    [Fact]
    public void InAppLinkPath_BackslashDisguisedAsSiteRelative_IsRejected()
    {
        // {{bookingLink}} must still appear somewhere (body or LinkPath) to satisfy rule 4's
        // required-placeholder check, so the malicious LinkPath's own rule is what fails this.
        var contents = FullVaccinationContents(overrideInApp: InApp("{{catName}}'s {{vaccineName}} is due {{dueOn}} {{bookingLink}}", linkPath: @"/\evil.test"));

        var violation = TemplateRules.Validate(VaccinationDefinition, contents);

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.InvalidTemplateLink, violation!.Code);
        Assert.StartsWith("Contents[2].LinkPath", violation.PropertyName);
    }

    /// <summary>The same bypass, on an email body's markdown link target (§8.2 rule 6).</summary>
    [Fact]
    public void EmailLinkTarget_BackslashDisguisedAsSiteRelative_IsRejected()
    {
        var contents = FullVaccinationContents(overrideEmail: Email(@"{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}} [Book](/\evil.test)"));

        var violation = TemplateRules.Validate(VaccinationDefinition, contents);

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.InvalidTemplateLink, violation!.Code);
    }

    [Fact]
    public void ValidateSampleOverride_RejectsOverlongValue()
    {
        var violation = TemplateRules.ValidateSampleOverride("catName", TemplateVariableKind.Text, new string('a', 501), "SampleValues.catName");

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.InvalidTemplateSampleValue, violation!.Code);
    }

    [Fact]
    public void ValidateSampleOverride_RejectsControlAndPrivateUseCharacters()
    {
        Assert.NotNull(TemplateRules.ValidateSampleOverride("catName", TemplateVariableKind.Text, "Mitu\0", "SampleValues.catName"));
        Assert.NotNull(TemplateRules.ValidateSampleOverride("catName", TemplateVariableKind.Text, "Mitu", "SampleValues.catName"));
    }

    [Fact]
    public void ValidateSampleOverride_RejectsNonHttpsUrl()
    {
        var violation = TemplateRules.ValidateSampleOverride("bookingLink", TemplateVariableKind.Url, "javascript:alert(1)", "SampleValues.bookingLink");

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.InvalidTemplateSampleValue, violation!.Code);
    }

    [Fact]
    public void ValidateSampleOverride_RejectsMalformedDate()
    {
        var violation = TemplateRules.ValidateSampleOverride("dueOn", TemplateVariableKind.Date, "junk", "SampleValues.dueOn");

        Assert.NotNull(violation);
        Assert.Equal(BusinessViolationCodes.InvalidTemplateSampleValue, violation!.Code);
    }

    [Fact]
    public void ValidateSampleOverride_AcceptsWellFormedValues()
    {
        Assert.Null(TemplateRules.ValidateSampleOverride("bookingLink", TemplateVariableKind.Url, "https://app.example.com/x", "p"));
        Assert.Null(TemplateRules.ValidateSampleOverride("dueOn", TemplateVariableKind.Date, "2027-01-12", "p"));
        Assert.Null(TemplateRules.ValidateSampleOverride("catName", TemplateVariableKind.Text, "Mitu", "p"));
    }
}

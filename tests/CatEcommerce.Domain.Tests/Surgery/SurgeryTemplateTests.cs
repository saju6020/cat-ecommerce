using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Xunit;

namespace CatEcommerce.Domain.Tests.Surgery;

/// <summary>HO-SRG-001 §8.10 -- U-B11: the six templates pass HO-NTF-001's template rules, carry the stated urgencies, and every SMS carries the link.</summary>
public sealed class SurgeryTemplateTests
{
    private static readonly string[] Keys =
    [
        NotificationTemplateCatalogue.SurgeryBooked, NotificationTemplateCatalogue.SurgeryPreOpReminder, NotificationTemplateCatalogue.SurgeryRescheduled,
        NotificationTemplateCatalogue.SurgeryCancelled, NotificationTemplateCatalogue.SurgeryPostOpInstructions, NotificationTemplateCatalogue.SurgeryFollowUpDue,
    ];

    private static NotificationTemplateDefinition Definition(string key) => NotificationTemplateCatalogue.Definitions.Single(d => d.Key == key);

    [Fact]
    public void U_B11_TheSixTemplates_AreInTheCatalogue_AndPassTemplateRules()
    {
        foreach (var key in Keys)
        {
            var definition = Definition(key);
            var contents = definition.Defaults.Select(d => new TemplateContentInput(d.Channel, d.Subject, d.Title, d.Body, d.LinkPath)).ToList();

            Assert.Null(TemplateRules.Validate(definition, contents));
            Assert.Equal(NotificationCategory.Transactional, definition.Category);
            Assert.Equal(3, definition.Channels.Count);
            Assert.Contains(NotificationChannel.InApp, definition.Channels);
            Assert.False(definition.ContainsSecret);
        }
    }

    [Fact]
    public void U_B11_TheUrgentOnes_AreBookedRescheduledCancelledAndPostOp()
    {
        var urgent = Keys.Where(k => Definition(k).Urgency == NotificationUrgency.Urgent).OrderBy(k => k).ToArray();

        Assert.Equal(NotificationTemplateCatalogue.UrgentSurgeryTemplateKeys.OrderBy(k => k).ToArray(), urgent);
        Assert.Equal(NotificationUrgency.Normal, Definition(NotificationTemplateCatalogue.SurgeryPreOpReminder).Urgency);
        Assert.Equal(NotificationUrgency.Normal, Definition(NotificationTemplateCatalogue.SurgeryFollowUpDue).Urgency);
    }

    [Fact]
    public void U_B11_EverySmsBody_CarriesTheLink_AndNeverTheInstructions()
    {
        foreach (var key in Keys)
        {
            var definition = Definition(key);
            var sms = definition.DefaultFor(NotificationChannel.Sms)!.Body;
            var urlVariables = definition.Variables.Where(v => v.Kind == TemplateVariableKind.Url).Select(v => v.Name).ToList();

            foreach (var url in urlVariables)
                Assert.Contains("{{" + url + "}}", sms);

            Assert.DoesNotContain("{{fastingInstructions}}", sms);
            Assert.DoesNotContain("{{followUpSummary}}", sms);
            Assert.True(sms.Length < 300, $"{key} SMS body is {sms.Length} characters (the limit after rendering is 480).");
        }
    }

    [Fact]
    public void ThePostOpAndBookedLinks_AreRequiredOnEveryChannel()
    {
        foreach (var key in new[] { NotificationTemplateCatalogue.SurgeryBooked, NotificationTemplateCatalogue.SurgeryPostOpInstructions })
        {
            var link = Definition(key).Variables.Single(v => v.Kind == TemplateVariableKind.Url);
            Assert.Equal(3, link.RequiredOn.Count);
        }
    }
}

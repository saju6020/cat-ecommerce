using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Ordering;

/// <summary>HO-ORD-001 §15 U-A7 -- the twelve order templates (§8.12, ADR-213).</summary>
public sealed class OrderTemplatesTests
{
    private static readonly string[] Keys =
    [
        "order.confirmed", "order.awaiting-transfer", "order.expired", "order.cancelled", "order.cancelled-refund-due", "order.dispatched",
        "order.tracking-added", "order.ready-for-collection", "order.delivered", "order.delivery-failed", "order.return-decided", "order.refund-issued",
    ];

    [Fact]
    public void U_A7_ThereAreExactlyTwelveOrderTemplates_InTheCatalogue()
    {
        var order = NotificationTemplateCatalogue.Definitions.Where(d => d.Key.StartsWith("order.", StringComparison.Ordinal)).Select(d => d.Key).OrderBy(k => k).ToList();
        Assert.Equal(Keys.OrderBy(k => k), order);
        Assert.Equal(NotificationTemplateCatalogue.Definitions.Count, NotificationTemplateCatalogue.Definitions.Select(d => d.Key).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void U_A7_EveryOrderTemplate_PassesTemplateRules_AndIsTransactional(string key)
    {
        var definition = NotificationTemplateCatalogue.Find(key)!;
        Assert.Equal(NotificationCategory.Transactional, definition.Category);
        Assert.False(definition.ContainsSecret);
        Assert.Equal(3, definition.Channels.Count);

        var contents = definition.Channels
            .Select(channel => definition.DefaultFor(channel)!)
            .Select(d => new TemplateContentInput(d.Channel, d.Subject, d.Title, d.Body, d.LinkPath))
            .ToList();

        Assert.Null(TemplateRules.Validate(definition, contents));
    }

    [Fact]
    public void U_A7_TheUrgentSet_IsExactlyConfirmedAwaitingTransferCancelledCancelledRefundDueAndRefundIssued()
    {
        var urgent = Keys.Where(k => NotificationTemplateCatalogue.Find(k)!.Urgency == NotificationUrgency.Urgent).OrderBy(k => k).ToList();
        Assert.Equal(
            new[] { "order.awaiting-transfer", "order.cancelled", "order.cancelled-refund-due", "order.confirmed", "order.refund-issued" }.OrderBy(k => k),
            urgent);
        Assert.Equal(urgent, NotificationTemplateCatalogue.UrgentOrderTemplateKeys.OrderBy(k => k));
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void NoOrderTemplate_CarriesATrackingUrl_OrAnExternalLinkVariable(string key)
    {
        var definition = NotificationTemplateCatalogue.Find(key)!;

        // Url variables come from the link builder: the only Url variable is the order link.
        Assert.All(definition.Variables.Where(v => v.Kind == TemplateVariableKind.Url), v => Assert.Equal("orderLink", v.Name));
        Assert.DoesNotContain(definition.Variables, v => v.Name.Contains("trackingUrl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheAwaitingTransferBankBranch_IsRequiredOnEmailAndInAppOnly()
    {
        var variable = NotificationTemplateCatalogue.Find(NotificationTemplateCatalogue.OrderAwaitingTransfer)!.Variables.Single(v => v.Name == "bankBranch");
        Assert.Equal([NotificationChannel.Email, NotificationChannel.InApp], variable.RequiredOn);
    }

    public static IEnumerable<object[]> AllKeys() => Keys.Select(k => new object[] { k });
}

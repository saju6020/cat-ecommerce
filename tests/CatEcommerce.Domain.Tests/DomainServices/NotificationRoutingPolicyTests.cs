using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-NTF-001 §15 U-B1..U-B10. Pure -- no database.</summary>
public sealed class NotificationRoutingPolicyTests
{
    private static readonly NotificationTemplateDefinition Transactional = NotificationTemplateCatalogue.Find(NotificationTemplateCatalogue.VaccinationDue)!;

    private static readonly NotificationTemplateDefinition Marketing = new(
        "shop.back-in-stock", "d", "d", NotificationCategory.Marketing, NotificationUrgency.Normal,
        [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], false,
        [], [
            new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null, "b", null),
            new NotificationTemplateChannelDefault(NotificationChannel.Email, "s", null, "b", null),
            new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "t", "b", null),
        ]);

    private static RecipientProfile Customer(
        bool isActive = true, string? mobile = "+8801700000000", string? email = "a@example.com",
        NotificationChannel? preferred = null, bool marketingGranted = false) =>
        new(NotificationRecipientKind.Customer, Guid.NewGuid(), isActive, "Jane", mobile, email,
            preferred ?? (mobile is not null ? NotificationChannel.Sms : NotificationChannel.Email), _ => marketingGranted);

    private static RecipientProfile Staff(bool isActive = true, string? mobile = "+8801700000000", string? email = "s@example.com") =>
        new(NotificationRecipientKind.Staff, Guid.NewGuid(), isActive, "Nurse", mobile, email, NotificationChannel.Email, _ => false);

    [Fact]
    public void Transactional_PreferredSms_BothIdentifiers_PrimarySms_InboxOn()
    {
        var recipient = Customer(preferred: NotificationChannel.Sms);
        var plan = NotificationRoutingPolicy.Plan(Transactional, recipient, null);

        Assert.Equal(NotificationChannel.Sms, plan.PrimaryChannel);
        Assert.True(plan.InboxCopy);
        Assert.Null(plan.SuppressionReason);
    }

    [Fact]
    public void Transactional_PreferredSms_NoMobile_FallsBackToEmail()
    {
        var recipient = Customer(mobile: null, preferred: NotificationChannel.Sms);
        var plan = NotificationRoutingPolicy.Plan(Transactional, recipient, null);

        Assert.Equal(NotificationChannel.Email, plan.PrimaryChannel);
    }

    [Fact]
    public void Transactional_IgnoresMarketingOptOut()
    {
        var recipient = Customer(marketingGranted: false, preferred: NotificationChannel.Sms);
        var plan = NotificationRoutingPolicy.Plan(Transactional, recipient, null);

        Assert.Equal(NotificationChannel.Sms, plan.PrimaryChannel);
    }

    [Fact]
    public void Marketing_ConsentEmailOnly_PreferredSms_PrimaryEmail()
    {
        var recipient = new RecipientProfile(NotificationRecipientKind.Customer, Guid.NewGuid(), true, "Jane",
            "+8801700000000", "a@example.com", NotificationChannel.Sms, c => c == NotificationChannel.Email);

        var plan = NotificationRoutingPolicy.Plan(Marketing, recipient, null);

        Assert.Equal(NotificationChannel.Email, plan.PrimaryChannel);
        Assert.False(plan.InboxCopy); // no in-app consent
    }

    [Fact]
    public void Marketing_NoConsentAnywhere_Suppressed()
    {
        var recipient = Customer(marketingGranted: false);
        var plan = NotificationRoutingPolicy.Plan(Marketing, recipient, null);

        Assert.Null(plan.PrimaryChannel);
        Assert.False(plan.InboxCopy);
        Assert.Equal(NotificationSuppressionReason.NoPermittedChannel, plan.SuppressionReason);
    }

    [Fact]
    public void SecretTemplate_NoInboxCopy()
    {
        var secretDefinition = NotificationTemplateCatalogue.Find(NotificationTemplateCatalogue.PasswordReset)!;
        var recipient = Customer();

        var plan = NotificationRoutingPolicy.Plan(secretDefinition, recipient, NotificationChannel.Sms);

        Assert.False(plan.InboxCopy);
    }

    [Fact]
    public void ChannelHint_Transactional_UsedFirst_Marketing_Ignored()
    {
        var recipient = Customer(preferred: NotificationChannel.Email);
        var transactionalPlan = NotificationRoutingPolicy.Plan(Transactional, recipient, NotificationChannel.Sms);
        Assert.Equal(NotificationChannel.Sms, transactionalPlan.PrimaryChannel);

        var marketingRecipient = new RecipientProfile(NotificationRecipientKind.Customer, Guid.NewGuid(), true, "Jane",
            "+8801700000000", "a@example.com", NotificationChannel.Email, _ => true);
        var marketingPlan = NotificationRoutingPolicy.Plan(Marketing, marketingRecipient, NotificationChannel.Sms);
        Assert.Equal(NotificationChannel.Email, marketingPlan.PrimaryChannel); // hint ignored for marketing
    }

    [Fact]
    public void StaffRecipient_NoInboxCopy_EmailBeforeSms()
    {
        var plan = NotificationRoutingPolicy.Plan(Transactional, Staff(), null);

        Assert.False(plan.InboxCopy);
        Assert.Equal(NotificationChannel.Email, plan.PrimaryChannel);
    }

    [Fact]
    public void NextFallback_AfterSms_ReturnsEmail_ThenNull()
    {
        var recipient = Customer(preferred: NotificationChannel.Sms);
        var afterSms = NotificationRoutingPolicy.NextFallback(Transactional, recipient, [NotificationChannel.Sms]);
        Assert.Equal(NotificationChannel.Email, afterSms);

        var afterBoth = NotificationRoutingPolicy.NextFallback(Transactional, recipient, [NotificationChannel.Sms, NotificationChannel.Email]);
        Assert.Null(afterBoth);
    }

    [Fact]
    public void Push_NeverSelected_EvenAsPreferredOrHint()
    {
        // Preferred/hint both (nonsensically) Push; both Sms and Email destinations exist.
        // Push is filtered by NotificationChannelRules.IsDeliverable, so the only remaining
        // candidate is Sms (the "other of Sms/Email" once Push isn't recognised as either).
        var recipient = Customer(preferred: NotificationChannel.Push);

        var plan = NotificationRoutingPolicy.Plan(Transactional, recipient, NotificationChannel.Push);

        Assert.NotEqual(NotificationChannel.Push, plan.PrimaryChannel);
        Assert.Equal(NotificationChannel.Sms, plan.PrimaryChannel);
    }
}

using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-ACC-001 §15, U-E8.</summary>
public sealed class NotificationPreferenceRulesTests
{
    [Fact]
    public void DefaultMarketingGranted_IsFalse() => Assert.False(NotificationPreferenceRules.DefaultMarketingGranted);

    [Fact]
    public void Transactional_IsAlwaysOnAndNotEditable()
    {
        Assert.True(NotificationPreferenceRules.IsAlwaysOn(NotificationCategory.Transactional));
        Assert.False(NotificationPreferenceRules.IsEditable(NotificationCategory.Transactional));
        Assert.True(NotificationPreferenceRules.Permits(NotificationChannel.Email, NotificationCategory.Transactional, marketingGranted: false));
    }

    [Fact]
    public void Marketing_IsEditableAndNotAlwaysOn()
    {
        Assert.False(NotificationPreferenceRules.IsAlwaysOn(NotificationCategory.Marketing));
        Assert.True(NotificationPreferenceRules.IsEditable(NotificationCategory.Marketing));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Marketing_PermitsOnlyWhenGranted(bool granted, bool expectedPermitted) =>
        Assert.Equal(expectedPermitted, NotificationPreferenceRules.Permits(NotificationChannel.Email, NotificationCategory.Marketing, granted));

    [Fact]
    public void GetAll_ReturnsEveryChannelTimesCategory()
    {
        var cells = NotificationPreferenceRules.GetAll().ToList();
        Assert.Equal(4 * 2, cells.Count); // 4 channels x 2 categories
    }
}

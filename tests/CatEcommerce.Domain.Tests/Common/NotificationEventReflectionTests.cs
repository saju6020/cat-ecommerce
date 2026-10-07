using System.Reflection;
using System.Text.RegularExpressions;
using CatEcommerce.Domain.Events.Notification;
using Platform.Infrastructure.Core.Events;
using Xunit;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-NTF-001 §15, U-X1. "Ids, keys, enums and booleans only -- no bodies, destinations
/// or variable values" (§11.5).</summary>
public sealed class NotificationEventReflectionTests
{
    private static readonly Regex Forbidden = new(
        "body|destination|variable|value|subject|title|token|link|mobile|phone|email|address", RegexOptions.IgnoreCase);

    [Fact]
    public void EveryNotificationEventType_HasNoDisallowedFreeTextStringProperty()
    {
        var eventTypes = typeof(NotificationTemplateCustomisedEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(NotificationTemplateCustomisedEvent).Namespace && typeof(Event).IsAssignableFrom(t) && !t.IsAbstract);

        var violations = new List<string>();

        foreach (var eventType in eventTypes)
        {
            foreach (var property in eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (property.PropertyType != typeof(string)) continue;
                if (Forbidden.IsMatch(property.Name))
                    violations.Add($"{eventType.Name}.{property.Name}");
            }
        }

        Assert.True(violations.Count == 0, "Disallowed properties found: " + string.Join(", ", violations));
    }
}

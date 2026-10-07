using System.Reflection;
using System.Text.RegularExpressions;
using CatEcommerce.Domain.Events.Account;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>
/// HO-ACC-001 §15, U-X1. Scoped to the Account events this handover owns (Events.Account) --
/// ids, booleans, enums and timestamps are exempt by design (§11.5: "ids, booleans, enums and
/// timestamps only"); this checks only free-text <c>string</c> properties, which are the ones
/// that could actually carry a name, identifier or secret. <see cref="CustomerMergedEvent.Reason"/>
/// is the one explicitly sanctioned free-text exception (§11.5).
/// </summary>
public sealed class EventPersonalDataReflectionTests
{
    private static readonly Regex Forbidden = new(
        "name|mobile|phone|email|address|line|password|hash|token|stamp", RegexOptions.IgnoreCase);

    private static readonly HashSet<(Type Type, string Property)> AllowList =
    [
        (typeof(CustomerMergedEvent), "Reason"),
    ];

    [Fact]
    public void EveryAccountEventType_HasNoDisallowedFreeTextStringProperty()
    {
        var eventTypes = typeof(CustomerRegisteredEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(CustomerRegisteredEvent).Namespace && typeof(Event).IsAssignableFrom(t) && !t.IsAbstract);

        var violations = new List<string>();

        foreach (var eventType in eventTypes)
        {
            foreach (var property in eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (property.PropertyType != typeof(string)) continue; // ids/booleans/enums/timestamps are exempt by design
                if (AllowList.Contains((eventType, property.Name))) continue;
                if (Forbidden.IsMatch(property.Name))
                    violations.Add($"{eventType.Name}.{property.Name}");
            }
        }

        Assert.True(violations.Count == 0, "Disallowed properties found: " + string.Join(", ", violations));
    }
}

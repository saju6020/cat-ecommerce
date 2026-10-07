using CatEcommerce.Domain.Events.Clinic;
using Platform.Infrastructure.Core.Events;
using Xunit;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-APT-001 §11.5/U-X1 -- ids, instants, enums and booleans only; no reason, complaint
/// or name text.</summary>
public sealed class ClinicEventsReflectionTests
{
    [Fact]
    public void EveryClinicEventType_HasNoStringProperty()
    {
        var eventTypes = typeof(AppointmentBookedEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(AppointmentBookedEvent).Namespace && typeof(Event).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(eventTypes);

        var violations = eventTypes
            .SelectMany(t => t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        Assert.True(violations.Count == 0, "Disallowed free-text properties found: " + string.Join(", ", violations));
    }
}

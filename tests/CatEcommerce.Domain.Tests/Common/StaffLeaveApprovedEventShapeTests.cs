using System.Reflection;
using CatEcommerce.Domain.Events.Administration;
using Xunit;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>
/// TEST gate seventh pass: U-C6, previously BLOCKED. <see cref="StaffLeaveApprovedEvent"/> must
/// carry only the four structured fields it is documented to (<c>LeaveRequestId</c>,
/// <c>StaffAccountId</c>, <c>StartsOn</c>, <c>EndsOn</c>) -- never the leave's own free-text
/// <c>Note</c>/<c>DecisionNote</c>, which any downstream consumer (a future notification handler)
/// would otherwise receive unfiltered. Contrast <c>EventPersonalDataReflectionTests</c> (HO-ACC-001's
/// own assembly-wide sweep for forbidden free-text *names*) -- this event carries no `string`
/// property at all, so that sweep's regex would never flag it even if a free-text field were added
/// here; a fixed field count is the correct check for this specific event.
/// </summary>
public sealed class StaffLeaveApprovedEventShapeTests
{
    [Fact]
    public void HasExactlyFourDeclaredProperties_NoFreeText()
    {
        var declaredProperties = typeof(StaffLeaveApprovedEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var expected = new[] { "EndsOn", "LeaveRequestId", "StaffAccountId", "StartsOn" }
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, declaredProperties);
        Assert.DoesNotContain(typeof(StaffLeaveApprovedEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            p => p.PropertyType == typeof(string));
    }
}

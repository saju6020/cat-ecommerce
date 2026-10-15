using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Surgery;

/// <summary>HO-SRG-001 §8.12 (ADR-167) -- reads a service protocol, or the shipped defaults when it has none. The text is SNAPSHOTTED into the surgery, so a later edit never
/// rewrites what an owner was given.</summary>
public static class SurgeryProtocols
{
    public static async Task<string> FastingInstructionsAsync(
        IAggregateRootRepository<SurgeryProtocolAggregate> repository, Guid serviceId, DateTimeOffset admitBy, string timeZoneId)
    {
        var protocol = await repository.GetByFilterAsync(p => p.ProtocolServiceId == serviceId);
        var template = protocol?.FastingInstructions ?? SurgeryProtocolDefaults.FastingInstructions;
        var text = template.Replace("{admitBy}", ClinicTime(admitBy, timeZoneId), StringComparison.Ordinal).Trim();
        return text.Length <= SurgeryRules.FastingInstructionsMax ? text : text[..SurgeryRules.FastingInstructionsMax];
    }

    /// <summary>The follow-up rules of a service: its protocol's, or the defaults.</summary>
    public static async Task<(IReadOnlyList<FollowUpRule> Rules, string PostOpInstructions)> DischargeProtocolAsync(
        IAggregateRootRepository<SurgeryProtocolAggregate> repository, Guid serviceId)
    {
        var protocol = await repository.GetByFilterAsync(p => p.ProtocolServiceId == serviceId);
        if (protocol is not null) return (protocol.Rules(), protocol.PostOpInstructions);

        return (SurgeryProtocolDefaults.FollowUps
            .Select(f => new FollowUpRule(Enumeration.FromDisplayName<SurgeryFollowUpKind>(f.Kind), f.DayOffset, f.WindowFromDay, f.WindowToDay)).ToList(),
            SurgeryProtocolDefaults.PostOpInstructions);
    }

    private static string ClinicTime(DateTimeOffset instant, string timeZoneId) =>
        TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}

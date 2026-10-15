using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Surgery;

/// <summary>
/// HO-SRG-001 §8.12 (ADR-167) -- one protocol per surgical service: fasting instructions and hours, post-op text and the follow-up rules (kind, day offset,
/// window). All text is plain text -- rendered as text by every consumer. The protocol is read at booking (fasting) and at discharge (post-op text,
/// follow-ups) and SNAPSHOTTED into the surgery, so a later edit never rewrites what an owner was given. The owned follow-up list is replaced wholesale on
/// every edit (a CurrentState carve-out, classified AggregateOnly).
/// </summary>
public sealed class SurgeryProtocolAggregate : AggregateRoot
{
    private readonly List<SurgeryProtocolFollowUp> _followUps = [];

    public Guid ProtocolServiceId { get; private set; }
    public string FastingInstructions { get; private set; } = default!;
    public int FastingHours { get; private set; }
    public string PostOpInstructions { get; private set; } = default!;
    public IReadOnlyList<SurgeryProtocolFollowUp> FollowUps => _followUps;
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedOn { get; private set; }

    private SurgeryProtocolAggregate() { }

    public SurgeryProtocolAggregate(Guid id) : base(id) { }

    /// <summary>Creates or replaces the protocol (the handler loads the existing one by service id, or mints a new aggregate).</summary>
    public void Upsert(SurgeryProtocolDto dto)
    {
        Validate(dto);

        var isNew = ProtocolServiceId == Guid.Empty;
        ProtocolServiceId = dto.ServiceId;
        FastingInstructions = dto.FastingInstructions.Trim();
        FastingHours = dto.FastingHours;
        PostOpInstructions = dto.PostOpInstructions.Trim();
        UpdatedBy = dto.UserContext.UserId;
        UpdatedOn = dto.Now;

        _followUps.Clear();
        foreach (var rule in dto.FollowUps)
            _followUps.Add(new SurgeryProtocolFollowUp(Guid.NewGuid(), rule.Kind, rule.DayOffset, rule.WindowFromDay, rule.WindowToDay));

        if (isNew) SetDefaultValue(dto.UserContext);
        UpdateVersion();
    }

    public IReadOnlyList<FollowUpRule> Rules() =>
        _followUps.Select(f => new FollowUpRule(f.Kind, f.DayOffset, f.WindowFromDay, f.WindowToDay)).ToList();

    private static void Validate(SurgeryProtocolDto dto)
    {
        if (dto.FastingHours is < 0 or > 24)
            throw Invalid("Fasting hours must be between 0 and 24.", nameof(dto.FastingHours));
        if (string.IsNullOrWhiteSpace(dto.FastingInstructions) || dto.FastingInstructions.Length > SurgeryRules.FastingInstructionsMax)
            throw Invalid($"Fasting instructions are required (up to {SurgeryRules.FastingInstructionsMax} characters).", nameof(dto.FastingInstructions));
        if (string.IsNullOrWhiteSpace(dto.PostOpInstructions) || dto.PostOpInstructions.Length > SurgeryRules.PostOpInstructionsMax)
            throw Invalid($"Post-op instructions are required (up to {SurgeryRules.PostOpInstructionsMax} characters).", nameof(dto.PostOpInstructions));
        if (dto.FollowUps.Count > SurgeryRules.MaxFollowUps)
            throw Invalid($"A protocol has at most {SurgeryRules.MaxFollowUps} follow-ups.", nameof(dto.FollowUps));

        foreach (var rule in dto.FollowUps)
            FollowUpPlanner.EnsureRuleValid(rule);
    }

    private static DomainRuleViolationException Invalid(string message, string property) =>
        new(BusinessViolationCodes.SurgeryProtocolInvalid, message, property);
}

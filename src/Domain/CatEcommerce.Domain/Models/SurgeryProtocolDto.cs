using CatEcommerce.Domain.DomainServices;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-SRG-001 §11.3 -- the input of <c>SurgeryProtocolAggregate.Upsert</c>.</summary>
public sealed record SurgeryProtocolDto(
    Guid ServiceId, string FastingInstructions, int FastingHours, string PostOpInstructions, IReadOnlyList<FollowUpRule> FollowUps,
    DateTimeOffset Now, UserContext UserContext);

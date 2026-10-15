using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Operations;

/// <summary>HO-XCT-001 section 11.5 -- ids, an enumeration and a date only.</summary>
public sealed class RestoreTestRecordedEvent : Event
{
    public required Guid RestoreTestId { get; init; }
    public required RestoreTestOutcome TestOutcome { get; init; }
    public required DateOnly PerformedOn { get; init; }
}

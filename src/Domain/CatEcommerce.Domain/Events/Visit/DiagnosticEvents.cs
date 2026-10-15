using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Visit;

public sealed class DiagnosticOrderedEvent : Event
{
    public required Guid DiagnosticOrderId { get; init; }
    public required Guid VisitId { get; init; }
    public required Guid CatId { get; init; }
    public required DiagnosticKind Kind { get; init; }
}

public sealed class DiagnosticResultsRecordedEvent : Event
{
    public required Guid DiagnosticOrderId { get; init; }
    public required Guid ResultSetId { get; init; }
    public required Guid CatId { get; init; }
    public required bool HasAbnormal { get; init; }
}

public sealed class DiagnosticFileAttachedEvent : Event
{
    public required Guid DiagnosticOrderId { get; init; }
    public required Guid AttachmentId { get; init; }
    public required Guid FileId { get; init; }
}

public sealed class DiagnosticOrderCancelledEvent : Event
{
    public required Guid DiagnosticOrderId { get; init; }
    public required Guid VisitId { get; init; }
}

using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Visit;

public sealed class ConsentRecordedEvent : Event
{
    public required Guid ConsentId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid AppointmentId { get; init; }
    public required ConsentKind Kind { get; init; }
    public required ConsentMethod Method { get; init; }
    public required int PolicyVersionNumber { get; init; }
}

public sealed class ConsentWithdrawnEvent : Event
{
    public required Guid ConsentId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid AppointmentId { get; init; }
}

// Catalogue events (ids only).
public sealed class DiagnosisCodeChangedEvent : Event
{
    public required Guid DiagnosisCodeId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class LabAnalyteChangedEvent : Event
{
    public required Guid LabAnalyteId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class NoteTemplateChangedEvent : Event
{
    public required Guid NoteTemplateId { get; init; }
    public required Guid ClinicServiceId { get; init; }
    public required int TemplateVersion { get; init; }
    public required bool IsActive { get; init; }
}

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-EMR-001 §8.5 -- a lab report file (PDF, JPEG, PNG or DICOM-as-file) with its date and
/// note. The bytes are HO-FIL-001's stored file (purpose <c>LabResult</c>); one file is attached once
/// (unique index). Append-only (trigger).</summary>
public sealed class DiagnosticAttachment
{
    public Guid Id { get; private set; }
    public Guid FileId { get; private set; }
    public DateOnly ResultDate { get; private set; }
    public string? Note { get; private set; }
    public Guid AttachedBy { get; private set; }
    public DateTimeOffset AttachedOn { get; private set; }

    private DiagnosticAttachment() { }

    public DiagnosticAttachment(Guid id, Guid fileId, DateOnly resultDate, string? note, Guid attachedBy, DateTimeOffset attachedOn)
    {
        Id = id;
        FileId = fileId;
        ResultDate = resultDate;
        Note = note;
        AttachedBy = attachedBy;
        AttachedOn = attachedOn;
    }
}

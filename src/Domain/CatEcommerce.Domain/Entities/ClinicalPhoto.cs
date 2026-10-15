namespace CatEcommerce.Domain.Entities;

/// <summary>HO-EMR-001 §8.2 -- a dated, noted clinical photograph on an open visit. The file itself
/// is HO-FIL-001's <c>StoredFileAggregate</c> (purpose <c>ClinicalPhoto</c>), referenced by id; one
/// file is attached once, enforced by a unique index on <c>file_id</c>.</summary>
public sealed class ClinicalPhoto
{
    public Guid Id { get; private set; }
    public Guid FileId { get; private set; }
    public DateOnly TakenOn { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset AddedOn { get; private set; }
    public Guid AddedBy { get; private set; }

    private ClinicalPhoto() { }

    public ClinicalPhoto(Guid id, Guid fileId, DateOnly takenOn, string? note, DateTimeOffset addedOn, Guid addedBy)
    {
        Id = id;
        FileId = fileId;
        TakenOn = takenOn;
        Note = note;
        AddedOn = addedOn;
        AddedBy = addedBy;
    }
}

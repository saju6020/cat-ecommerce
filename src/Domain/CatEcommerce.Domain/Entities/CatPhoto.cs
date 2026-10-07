namespace CatEcommerce.Domain.Entities;

/// <summary>
/// Gate-fix 2026-09-27 (ARCHITECTURE HIGH, HO-FIL-001-ARCHITECTURE.md): references the uploaded
/// file by id only (.ai/bounded-contexts.md rule 2, "aggregates reference each other by id") --
/// content type, size and the original file name are <c>StoredFileAggregate</c>'s own state, read
/// at serve time through <c>GetCatProfileQueryHandler</c>'s own lookup, not copied here where they
/// could go stale the moment FIL's purge or erasure runs.
/// </summary>
public sealed class CatPhoto
{
    public Guid Id { get; private set; }
    public Guid FileId { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTimeOffset UploadedOn { get; private set; }

    private CatPhoto() { }

    public CatPhoto(Guid id, Guid fileId, bool isPrimary, DateTimeOffset uploadedOn)
    {
        Id = id;
        FileId = fileId;
        IsPrimary = isPrimary;
        UploadedOn = uploadedOn;
    }

    /// <summary>Called when a newly added photo takes over as the primary — exactly one primary per cat.</summary>
    internal void ClearPrimary() => IsPrimary = false;
}

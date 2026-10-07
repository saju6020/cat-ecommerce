namespace CatEcommerce.Api.Contracts.Cat;

/// <summary>
/// US-CAT-01 AC3 / NFR-SEC-5. Closes the HO-CAT-002-SECURITY carried item (HO-FIL-001 §1 Amended):
/// the client uploads through <c>POST /api/staff/files?purpose=CatProfilePhoto</c> first (server
/// scans, stores and mints the file id) and supplies only that <see cref="FileId"/> here -- never a
/// storage key, content type or size. The handler pulls those from the verified
/// <c>StoredFileAggregate</c> row instead of trusting the caller.
/// </summary>
public sealed class AddCatPhotoRequest
{
    public required Guid FileId { get; init; }
    public bool IsPrimary { get; init; }
}

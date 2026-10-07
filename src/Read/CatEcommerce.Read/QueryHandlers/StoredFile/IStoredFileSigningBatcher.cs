using CatEcommerce.Read.ViewModels;

namespace CatEcommerce.Read.QueryHandlers.StoredFile;

/// <summary>
/// Gate-fix 2026-09-27 (ARCHITECTURE LOW, HO-FIL-001-ARCHITECTURE.md second pass): a FIL-owned
/// helper for "given several file ids, return the §12.4 signed shape for each" -- the exact lookup
/// <c>GetCatProfileQueryHandler</c> re-implemented against <c>files.stored_file</c> directly for
/// its whole photo list (rather than dispatching <see cref="Queries.StoredFile.GetStoredFileQuery"/>
/// once per photo, which ARCHITECTURE's own MEDIUM finding already forbids). Kept "owned once"
/// here so the next consuming read (HO-EMR-001, first in line) does not copy the same
/// metadata-plus-sign mapping into its own handler.
/// </summary>
public interface IStoredFileSigningBatcher
{
    /// <summary>Files with no matching row (purged, erased, or never registered) are simply absent
    /// from the result -- the caller decides what "no photo to show" means for its own view model,
    /// this helper does not guess.</summary>
    Task<IReadOnlyDictionary<Guid, StoredFileViewModel>> SignManyAsync(
        IReadOnlyCollection<Guid> fileIds, string requestedDisposition, CancellationToken ct = default);
}

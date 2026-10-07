using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CatEcommerce.Infrastructure.Files;

/// <summary>HO-FIL-001 §8.7: object first, row second. Shared by
/// <c>DeleteStoredFileCommandHandler</c> and <see cref="StoredFilePurgeSource"/>.
///
/// Gate-fix 2026-09-27 (ARCHITECTURE HIGH): <c>LegalHold</c> is enforced HERE, not only by each
/// caller -- "a held file is never purged" (§8.1) is a rule about this file's own state, so the one
/// path that actually deletes is where it is authoritative. Callers may still check it earlier
/// too (to give a clearer response), but this is the backstop that cannot be bypassed by a caller
/// that forgets to.</summary>
public sealed class StoredFileEraser(
    CatEcommerceDbContext db,
    IFileStore fileStore,
    ILogger<StoredFileEraser> logger) : IStoredFileEraser
{
    public async Task<bool> EraseAsync(Guid fileId, CancellationToken ct)
    {
        var row = await db.StoredFiles.FirstOrDefaultAsync(f => f.Id == fileId, ct);
        if (row is null) return false;

        if (row.LegalHold)
        {
            logger.LogWarning("Refused to erase stored file {FileId}: under legal hold.", fileId);
            return false;
        }

        var deleteResult = await fileStore.DeleteAsync(row.StorageKey, ct);
        if (!deleteResult.IsSuccess)
        {
            // A row with a live object we failed to delete must stay visible, not be dropped --
            // §8.7: "a deleted row with a live object is an orphan nobody will ever find".
            logger.LogError(
                "Failed to delete object for stored file {FileId} at storage key {StorageKey}: {Code} {Message}",
                fileId, row.StorageKey, deleteResult.ViolationCode, deleteResult.Message);
            throw new InvalidOperationException(
                $"Failed to delete the stored object for file '{fileId}' ({deleteResult.ViolationCode}).");
        }

        db.StoredFiles.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }
}

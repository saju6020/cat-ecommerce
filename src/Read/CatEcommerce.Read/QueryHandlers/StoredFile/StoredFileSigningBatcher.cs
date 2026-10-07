using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.StoredFile;

/// <summary>See <see cref="IStoredFileSigningBatcher"/>. One query against
/// <c>files.stored_file</c> for every requested id, then one <see cref="IFileUrlSigner.Sign"/> call
/// per file -- no N+1 dispatch.</summary>
public sealed class StoredFileSigningBatcher(
    CatEcommerceDbContext db,
    IFileUrlSigner signer,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : IStoredFileSigningBatcher
{
    public async Task<IReadOnlyDictionary<Guid, StoredFileViewModel>> SignManyAsync(
        IReadOnlyCollection<Guid> fileIds, string requestedDisposition, CancellationToken ct = default)
    {
        if (fileIds.Count == 0) return new Dictionary<Guid, StoredFileViewModel>();

        var rows = await db.StoredFiles.AsNoTracking()
            .Where(f => fileIds.Contains(f.Id))
            .Select(f => new { f.Id, f.ContentType, f.SizeInBytes, f.OriginalFileName })
            .ToListAsync(ct);

        var signedUrlMinutes = businessSettings.GetInt(SettingKeys.FilesSignedUrlMinutes);
        var expiresIn = TimeSpan.FromMinutes(Math.Max(1, signedUrlMinutes));
        // Gate-fix 2026-09-27 (ARCHITECTURE LOW, third pass): TimeProvider, not DateTimeOffset.UtcNow.
        var urlExpiresOn = timeProvider.GetUtcNow().Add(expiresIn);

        var result = new Dictionary<Guid, StoredFileViewModel>(rows.Count);
        foreach (var row in rows)
        {
            var disposition = FileServingRules.ResolveDisposition(row.ContentType, requestedDisposition);
            var url = signer.Sign(row.Id, expiresIn, disposition);
            result[row.Id] = new StoredFileViewModel(row.Id, url, row.ContentType, row.SizeInBytes, row.OriginalFileName, urlExpiresOn);
        }

        return result;
    }
}

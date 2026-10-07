using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Cat;

/// <summary>
/// Closes the HO-CAT-002-SECURITY carried item (HO-FIL-001 §1 Amended): carries only a
/// server-issued <see cref="FileId"/> from HO-FIL-001's upload endpoint -- never a
/// client-supplied storage key, content type, size or file name. The handler verifies the file
/// (purpose, ownership, freshness) and reads its real metadata from
/// <c>StoredFileAggregate</c> before building <c>CatPhotoDto</c>.
/// </summary>
public sealed class AddCatPhotoCommand : Command
{
    public required Guid CatId { get; init; }
    public required Guid PhotoId { get; init; }
    public required Guid FileId { get; init; }
    public required bool IsPrimary { get; init; }
}

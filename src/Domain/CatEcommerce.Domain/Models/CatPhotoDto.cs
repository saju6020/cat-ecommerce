using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>Gate-fix 2026-09-27: carries only the photo id, the already-verified <c>FileId</c> and
/// <c>IsPrimary</c> -- content type/size/name are FIL's own state, not copied here.</summary>
public sealed record CatPhotoDto(
    Guid PhotoId,
    Guid FileId,
    bool IsPrimary,
    DateTimeOffset UploadedOn,
    UserContext UserContext,
    Guid CorrelationId);

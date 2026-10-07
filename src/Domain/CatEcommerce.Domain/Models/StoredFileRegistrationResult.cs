namespace CatEcommerce.Domain.Models;

/// <summary>
/// Gate-fix 2026-09-27 (ARCHITECTURE MEDIUM, cqrs.md §5 rule 1: "a controller dispatches exactly
/// one command or one query"). <c>RegisterStoredFileCommandHandler</c>'s own
/// <c>CommandResponse.Result</c> -- the §12.4 signed-file shape -- so
/// <c>FilesController</c>'s upload action no longer has to dispatch <c>GetStoredFileQuery</c>
/// afterwards just to build the same response. Defined in Domain, not
/// <c>CatEcommerce.Read.ViewModels</c>, because Application must never reference Read
/// (.ai/read-side-pattern.md); the controller returns this directly, which happens to be the same
/// shape as <c>StoredFileViewModel</c>.
/// </summary>
public sealed record StoredFileRegistrationResult(
    Guid FileId,
    string Url,
    string ContentType,
    long SizeInBytes,
    string OriginalFileName,
    DateTimeOffset UrlExpiresOn);

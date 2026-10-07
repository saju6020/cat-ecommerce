namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-FIL-001 §12.4 -- the contract for HO-FIL-002: "the signed-URL view model
/// { fileId, url, contentType, sizeInBytes, originalFileName, urlExpiresOn }. Every consuming area
/// returns this shape for a file."</summary>
public sealed record StoredFileViewModel(
    Guid FileId,
    string Url,
    string ContentType,
    long SizeInBytes,
    string OriginalFileName,
    DateTimeOffset UrlExpiresOn);

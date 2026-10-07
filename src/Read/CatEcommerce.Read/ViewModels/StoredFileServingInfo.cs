namespace CatEcommerce.Read.ViewModels;

/// <summary>
/// Internal to <c>FilesController</c>'s serving endpoint -- carries the real
/// <see cref="StorageKey"/>, unlike <see cref="StoredFileViewModel"/> (which never leaves the
/// process, ADR-318). Never serialised directly to a client.
/// </summary>
public sealed record StoredFileServingInfo(string StorageKey, string ContentType, string OriginalFileName, long SizeInBytes);

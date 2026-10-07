using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.StoredFile;

/// <summary>HO-FIL-001 §8.2 step 7: saves the aggregate after every upload-pipeline check
/// (steps 1-6) has already passed. Every field here is already verified server-side -- there is
/// no client-supplied storage key, content type or size on this command.</summary>
public sealed class RegisterStoredFileCommand : Command
{
    public required Guid FileId { get; init; }
    public required FilePurpose Purpose { get; init; }
    public required string ContentType { get; init; }
    public required long SizeInBytes { get; init; }
    public required string Sha256 { get; init; }
    public required string OriginalFileName { get; init; }
    public required string StorageKey { get; init; }
    public required Guid UploadedBy { get; init; }
    public required DateTimeOffset UploadedOn { get; init; }
    public required string ScannerName { get; init; }
    public DateOnly? RetainUntil { get; init; }
}

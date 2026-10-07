using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.StoredFile;

/// <summary>HO-FIL-001 §8.2 step 5: "one audit entry with the signature name and no content" when
/// a scan finds infected content, or when the scanner is unavailable. The upload never became an
/// aggregate, so there is no domain event to hang this on -- recorded directly, mirroring
/// <c>RecordAuthorizationDenialCommand</c> (HO-ACC-001).</summary>
public sealed class RecordFileRejectionAuditCommand : Command
{
    public required Guid AttemptedFileId { get; init; }
    public required string ViolationCode { get; init; }
    public required string Purpose { get; init; }
    public string? ScannerName { get; init; }
    public string? IpAddress { get; init; }
}

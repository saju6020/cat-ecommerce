using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-FIL-001 §8.1/§11.3 -- <c>RegisterStoredFileCommand</c>'s mapping onto
/// <c>StoredFileAggregate.Register</c>. Every field here was already validated by the upload
/// pipeline (§8.2 steps 1-6) before the command is dispatched.</summary>
public sealed record StoredFileRegistrationDto(
    Guid FileId,
    FilePurpose Purpose,
    string ContentType,
    long SizeInBytes,
    string Sha256,
    string OriginalFileName,
    string StorageKey,
    Guid UploadedBy,
    DateTimeOffset UploadedOn,
    string ScannerName,
    DateOnly? RetainUntil,
    UserContext UserContext,
    Guid CorrelationId);

using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// <c>IFileStore.PutAsync</c>'s request (ADR-318). No handover gives this record's fields; inferred
/// from ADR-318/HO-FIL-001 §8.4's description of <c>GcsFileStore.PutAsync</c> ("from the temp-file
/// stream, with <c>ContentType</c>, ... and <c>Metadata</c> carrying <c>fileId</c>, <c>purpose</c>
/// and <c>sha256</c>. Key from §8.1") and §8.1's <c>StoredFileAggregate</c> fields (<c>StorageKey</c>
/// is server-generated <c>{purpose}/{yyyy}/{MM}/{id:N}</c>, never derived from the uploaded name).
/// Flagged as inferred in docs/handover/CONTRACTS.md.
/// </summary>
public sealed record FilePutRequest(
    Guid FileId,
    FilePurpose Purpose,
    Stream Content,
    string ContentType,
    long SizeInBytes,
    string Sha256);

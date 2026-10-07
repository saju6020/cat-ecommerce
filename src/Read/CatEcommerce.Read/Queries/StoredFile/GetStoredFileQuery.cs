using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.StoredFile;

/// <summary>HO-FIL-001 §12: metadata plus a signed URL, for a consuming read to embed in its own
/// view model (§12.4).</summary>
public sealed class GetStoredFileQuery : Query<StoredFileViewModel?>
{
    public required Guid FileId { get; init; }

    /// <summary>"inline" or "attachment" -- only honoured for a content type §8.6 allows inline
    /// for; anything else (including an unrecognised value) resolves to "attachment".</summary>
    public string RequestedDisposition { get; init; } = FileServingRules.Attachment;
}

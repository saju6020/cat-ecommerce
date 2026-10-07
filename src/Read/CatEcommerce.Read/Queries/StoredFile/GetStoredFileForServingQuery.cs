using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.StoredFile;

/// <summary>Used only by <c>FilesController.GetFileContent</c> -- never exposed as a DTO to a
/// client (§8.6: the storage key never leaves the process).</summary>
public sealed class GetStoredFileForServingQuery : Query<StoredFileServingInfo?>
{
    public required Guid FileId { get; init; }
}

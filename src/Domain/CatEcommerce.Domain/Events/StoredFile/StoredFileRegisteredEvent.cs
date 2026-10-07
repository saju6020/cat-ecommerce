using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.StoredFile;

public sealed class StoredFileRegisteredEvent : Event
{
    public required Guid FileId { get; init; }
    public required string Purpose { get; init; }
    public required Guid UploadedBy { get; init; }
}

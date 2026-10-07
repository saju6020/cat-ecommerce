using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.StoredFile;

/// <summary>HO-FIL-001 §11.3. A permanent, object-then-row delete (§8.7) -- not exposed on a
/// public route in this slice (no consuming area calls it yet); dispatched internally by a
/// future consuming area that needs to explicitly retire a file it no longer references.</summary>
public sealed class DeleteStoredFileCommand : Command
{
    public required Guid FileId { get; init; }
}

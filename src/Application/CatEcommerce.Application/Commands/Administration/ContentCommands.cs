using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Administration;

public sealed class CreateContentItemCommand : Command
{
    public required Guid ContentItemId { get; init; }
    public required string Kind { get; init; }
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string DraftBody { get; init; }
    public string? Placement { get; init; }
    public string? LinkUrl { get; init; }
    public DateTimeOffset? VisibleFrom { get; init; }
    public DateTimeOffset? VisibleTo { get; init; }
    public int? SortOrder { get; init; }
}

public sealed class EditContentDraftCommand : Command
{
    public required Guid ContentItemId { get; init; }
    public required string Title { get; init; }
    public required string DraftBody { get; init; }
    public string? Placement { get; init; }
    public string? LinkUrl { get; init; }
    public DateTimeOffset? VisibleFrom { get; init; }
    public DateTimeOffset? VisibleTo { get; init; }
    public int? SortOrder { get; init; }
}

public sealed class PublishContentItemCommand : Command
{
    public required Guid ContentItemId { get; init; }
}

public sealed class UnpublishContentItemCommand : Command
{
    public required Guid ContentItemId { get; init; }
}

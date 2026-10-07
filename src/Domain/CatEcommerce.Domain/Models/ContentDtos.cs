using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record ContentItemCreateDto(
    ContentKind Kind, string Slug, string Title, string DraftBody, BannerPlacement? Placement,
    string? LinkUrl, DateTimeOffset? VisibleFrom, DateTimeOffset? VisibleTo, int? SortOrder,
    UserContext UserContext, Guid CorrelationId);

public sealed record ContentItemDraftEditDto(
    string Title, string DraftBody, BannerPlacement? Placement, string? LinkUrl,
    DateTimeOffset? VisibleFrom, DateTimeOffset? VisibleTo, int? SortOrder,
    UserContext UserContext, Guid CorrelationId);

public sealed record ContentItemPublishDto(DateTimeOffset PublishedOn, UserContext UserContext, Guid CorrelationId);

public sealed record ContentItemUnpublishDto(UserContext UserContext, Guid CorrelationId);

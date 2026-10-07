using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>HO-ADM-001 §8.8 / ADR-052. Bodies are <see cref="LimitedMarkup"/>, never HTML.</summary>
public sealed class ContentItemAggregate : AggregateRoot
{
    public ContentKind Kind { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string DraftBody { get; private set; } = default!;
    public string? PublishedBody { get; private set; }
    public DateTimeOffset? PublishedOn { get; private set; }
    public ContentStatus Status { get; private set; } = default!;
    public BannerPlacement? Placement { get; private set; }
    public string? LinkUrl { get; private set; }
    public DateTimeOffset? VisibleFrom { get; private set; }
    public DateTimeOffset? VisibleTo { get; private set; }
    public int? SortOrder { get; private set; }

    private ContentItemAggregate() { }

    public ContentItemAggregate(Guid id) : base(id) { }

    public void Create(ContentItemCreateDto dto)
    {
        ValidateSlug(dto.Slug);
        ValidateTitle(dto.Title);
        ValidateBody(dto.DraftBody);
        ValidateLink(dto.LinkUrl);

        Kind = dto.Kind;
        Slug = dto.Slug;
        Title = dto.Title;
        DraftBody = dto.DraftBody;
        Status = ContentStatus.Draft;
        Placement = dto.Placement;
        LinkUrl = dto.LinkUrl;
        VisibleFrom = dto.VisibleFrom;
        VisibleTo = dto.VisibleTo;
        SortOrder = dto.SortOrder;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new ContentItemCreatedEvent { ContentItemId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void EditDraft(ContentItemDraftEditDto dto)
    {
        ValidateTitle(dto.Title);
        ValidateBody(dto.DraftBody);
        ValidateLink(dto.LinkUrl);

        Title = dto.Title;
        DraftBody = dto.DraftBody;
        Placement = dto.Placement;
        LinkUrl = dto.LinkUrl;
        VisibleFrom = dto.VisibleFrom;
        VisibleTo = dto.VisibleTo;
        SortOrder = dto.SortOrder;

        AddDomainEvent(new ContentItemDraftEditedEvent { ContentItemId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>Copies draft to published. Editing the draft afterwards does not change
    /// <see cref="PublishedBody"/> (U-F3).</summary>
    public void Publish(ContentItemPublishDto dto)
    {
        PublishedBody = DraftBody;
        PublishedOn = dto.PublishedOn;
        Status = ContentStatus.Published;

        AddDomainEvent(new ContentItemPublishedEvent { ContentItemId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Unpublish(ContentItemUnpublishDto dto)
    {
        Status = ContentStatus.Unpublished;

        AddDomainEvent(new ContentItemUnpublishedEvent { ContentItemId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    private static void ValidateSlug(string slug)
    {
        // Gate fix 2026-09-26: the same shared predicate the FluentValidation validator now checks
        // first, before the handler's own uniqueness pre-check ever runs -- this call is retained
        // as the aggregate's own invariant, in case a future caller constructs it without going
        // through the validated command path.
        if (!SlugFormat.IsValid(slug))
            throw new DomainRuleViolationException(BusinessViolationCodes.ContentSlugAlreadyExists, "The slug is invalid.", nameof(Slug));
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            throw new DomainRuleViolationException(BusinessViolationCodes.NameRequired, "A title is required.", nameof(Title));
    }

    private static void ValidateBody(string body)
    {
        var error = LimitedMarkup.Validate(body);
        if (error is not null) throw new DomainRuleViolationException(error, "The content body contains disallowed markup.", nameof(DraftBody));
    }

    private static void ValidateLink(string? linkUrl)
    {
        if (linkUrl is null) return;

        var isHttps = linkUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        var isSiteRelative = linkUrl.StartsWith('/') && !linkUrl.StartsWith("//", StringComparison.Ordinal);

        if (!isHttps && !isSiteRelative)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidContentLink, "The link must be https or site-relative.", nameof(LinkUrl));
    }
}

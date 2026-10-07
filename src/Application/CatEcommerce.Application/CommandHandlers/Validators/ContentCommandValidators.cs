using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class CreateContentItemCommandValidator : AbstractValidator<CreateContentItemCommand>
{
    public CreateContentItemCommandValidator()
    {
        RuleFor(x => x.ContentItemId).NotEmpty();
        // Gate fix 2026-09-26 (SEC LOW, hardening round): unknown/wrongly-cased -> 400, not a 500
        // out of AdministrationMapper's Enumeration.FromDisplayName.
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<CreateContentItemCommand, ContentKind>();
        // Gate fix 2026-09-26 (SEC LOW): the slug regex previously lived only inside
        // ContentItemAggregate.Create, called after the handler's own ExistAsync uniqueness
        // pre-check already sent a NUL-containing slug to Postgres (22021, uncaught). Checked here
        // instead, before the handler runs at all.
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(80).MustBeSlugFormat();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.DraftBody).NotNull();
        RuleFor(x => x.Placement).MustBeValidEnumeration<CreateContentItemCommand, BannerPlacement>();
        // Gate fix 2026-09-26 (SEC LOW, hardening round): a NUL byte previously reached Postgres
        // unfiltered -- reachable by a Shop Manager.
        RuleFor(x => x.LinkUrl).NoControlCharacters();
    }
}

public sealed class EditContentDraftCommandValidator : AbstractValidator<EditContentDraftCommand>
{
    public EditContentDraftCommandValidator()
    {
        RuleFor(x => x.ContentItemId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.DraftBody).NotNull();
        RuleFor(x => x.Placement).MustBeValidEnumeration<EditContentDraftCommand, BannerPlacement>();
        RuleFor(x => x.LinkUrl).NoControlCharacters();
    }
}

public sealed class PublishContentItemCommandValidator : AbstractValidator<PublishContentItemCommand>
{
    public PublishContentItemCommandValidator() => RuleFor(x => x.ContentItemId).NotEmpty();
}

public sealed class UnpublishContentItemCommandValidator : AbstractValidator<UnpublishContentItemCommand>
{
    public UnpublishContentItemCommandValidator() => RuleFor(x => x.ContentItemId).NotEmpty();
}

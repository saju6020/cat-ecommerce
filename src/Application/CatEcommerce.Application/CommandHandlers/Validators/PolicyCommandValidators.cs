using CatEcommerce.Application.Commands.Administration;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class CreatePolicyDocumentCommandValidator : AbstractValidator<CreatePolicyDocumentCommand>
{
    public CreatePolicyDocumentCommandValidator()
    {
        RuleFor(x => x.PolicyDocumentId).NotEmpty();
        // Gate fix 2026-09-26 (SEC LOW): the same lowercase-kebab format as a content slug, checked
        // here -- before the handler's own ExistAsync uniqueness pre-check ever runs -- so a NUL
        // byte or a whitespace/Unicode lookalike key never reaches Postgres.
        RuleFor(x => x.Key).NotEmpty().MaximumLength(80).MustBeSlugFormat();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).NoControlCharacters();
    }
}

public sealed class PublishPolicyVersionCommandValidator : AbstractValidator<PublishPolicyVersionCommand>
{
    public PublishPolicyVersionCommandValidator()
    {
        RuleFor(x => x.PolicyVersionId).NotEmpty();
        RuleFor(x => x.PolicyKey).NotEmpty().MustBeSlugFormat();
        RuleFor(x => x.Body).NotNull();
        RuleFor(x => x.ChangeSummary).NotNull().MaximumLength(500).NoControlCharacters();
    }
}

public sealed class AcceptPolicyVersionCommandValidator : AbstractValidator<AcceptPolicyVersionCommand>
{
    public AcceptPolicyVersionCommandValidator()
    {
        // Gate fix 2026-09-26 (SEC LOW): customer-reachable (POST /api/customers/me/policy-acceptances)
        // -- the same format check as create/publish, before the handler's own document lookup query.
        RuleFor(x => x.PolicyKey).NotEmpty().MustBeSlugFormat();
        RuleFor(x => x.VersionNumber).GreaterThan(0);
    }
}

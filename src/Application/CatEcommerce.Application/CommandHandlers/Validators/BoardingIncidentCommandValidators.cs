using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class AddStayMediaCommandValidator : AbstractValidator<AddStayMediaCommand>
{
    public AddStayMediaCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.FileId).NotEmpty();
        RuleFor(x => x.Caption).MaximumLength(IncidentRules.MaxCaptionLength).NoControlCharacters();
    }
}

public sealed class HideStayMediaCommandValidator : AbstractValidator<HideStayMediaCommand>
{
    public HideStayMediaCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(IncidentRules.MaxSkipReasonLength).NoControlCharacters();
    }
}

public sealed class RecordIncidentCommandValidator : AbstractValidator<RecordIncidentCommand>
{
    public RecordIncidentCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEmpty();
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<RecordIncidentCommand, IncidentKind>();
        RuleFor(x => x.Severity).NotEmpty().MustBeValidEnumeration<RecordIncidentCommand, IncidentSeverity>();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(IncidentRules.MaxDescriptionLength).NoControlCharacters();
        RuleFor(x => x.ImmediateAction).NotEmpty().MaximumLength(IncidentRules.MaxImmediateActionLength).NoControlCharacters();
    }
}

public sealed class AcknowledgeIncidentCommandValidator : AbstractValidator<AcknowledgeIncidentCommand>
{
    public AcknowledgeIncidentCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEmpty();
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}

public sealed class RecordIncidentAcknowledgementCommandValidator : AbstractValidator<RecordIncidentAcknowledgementCommand>
{
    public RecordIncidentAcknowledgementCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class EscalateIncidentCommandValidator : AbstractValidator<EscalateIncidentCommand>
{
    public EscalateIncidentCommandValidator() => RuleFor(x => x.IncidentId).NotEmpty();
}

public sealed class RecordIncidentPhoneCallCommandValidator : AbstractValidator<RecordIncidentPhoneCallCommand>
{
    public RecordIncidentPhoneCallCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Outcome).NotEmpty().MaximumLength(IncidentRules.MaxPhoneOutcomeLength).NoControlCharacters();
    }
}

public sealed class ResolveIncidentCommandValidator : AbstractValidator<ResolveIncidentCommand>
{
    public ResolveIncidentCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Resolution).NotEmpty().MaximumLength(IncidentRules.MaxResolutionLength).NoControlCharacters();
    }
}

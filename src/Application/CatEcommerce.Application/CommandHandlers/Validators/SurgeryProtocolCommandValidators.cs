using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-SRG-001 §11.2 -- shape only. The follow-up windows and the service being a theatre procedure are aggregate / handler rules.</summary>
public sealed class UpsertSurgeryProtocolCommandValidator : AbstractValidator<UpsertSurgeryProtocolCommand>
{
    public UpsertSurgeryProtocolCommandValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.FastingInstructions).NotEmpty().MaximumLength(SurgeryRules.FastingInstructionsMax).NoControlCharacters();
        RuleFor(x => x.FastingHours).InclusiveBetween(0, 24);
        RuleFor(x => x.PostOpInstructions).NotEmpty().MaximumLength(SurgeryRules.PostOpInstructionsMax).NoControlCharacters();
        RuleFor(x => x.FollowUps).NotNull();
        RuleForEach(x => x.FollowUps).ChildRules(f => f.RuleFor(i => i.Kind).MustBeValidEnumeration<SurgeryProtocolFollowUpInput, SurgeryFollowUpKind>());
    }
}

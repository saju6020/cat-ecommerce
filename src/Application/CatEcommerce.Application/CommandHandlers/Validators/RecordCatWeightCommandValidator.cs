using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Domain.ValueObjects;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordCatWeightCommandValidator : AbstractValidator<RecordCatWeightCommand>
{
    public RecordCatWeightCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.WeightRecordId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        // ARCHITECTURE/CODE_REVIEW gates, LOW (2026-09-26, second pass): restored as defence in
        // depth now that [RequireAction(Actions.Cats.RecordClinical)] landed on this action
        // (CatsController.cs, HO-ADM-001 §12.3 / ADR-043 merge, ac79f82) -- RecordClinical is
        // staff-only (not in CustomerActions.Cat), so a caller that reaches this validator is
        // always an authenticated staff member with a real UserContext.UserId; RequireActionFilter
        // refuses an anonymous or unauthenticated caller with 401 before this validator ever runs,
        // so NotEmpty() never legitimately fires today. It catches a future regression in the
        // controller's own RecordedByStaffId = UserContext.UserId assignment (SECURITY gate,
        // MEDIUM, 2026-09-26 -- RecordedByStaffId is not client-suppliable) rather than silently
        // persisting a Guid.Empty actor.
        RuleFor(x => x.RecordedByStaffId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);

        RuleFor(x => x.Kilograms).InclusiveBetween(Weight.MinKilograms, Weight.MaxKilograms);

        RuleFor(x => x.RecordedOn)
            .LessThanOrEqualTo(_ => CatRecordDateBound.LatestPossibleLocalDate(timeProvider))
            .WithMessage("The weight record date cannot be in the future.");
    }
}

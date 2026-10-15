using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class ConfirmBoardingStayCommandValidator : AbstractValidator<ConfirmBoardingStayCommand>
{
    public ConfirmBoardingStayCommandValidator() => RuleFor(x => x.StayId).NotEmpty();
}

public sealed class LapseBoardingReservationCommandValidator : AbstractValidator<LapseBoardingReservationCommand>
{
    public LapseBoardingReservationCommandValidator() => RuleFor(x => x.StayId).NotEmpty();
}

public sealed class ExpireBoardingEnquiryCommandValidator : AbstractValidator<ExpireBoardingEnquiryCommand>
{
    public ExpireBoardingEnquiryCommandValidator() => RuleFor(x => x.StayId).NotEmpty();
}

public sealed class CancelBoardingStayCommandValidator : AbstractValidator<CancelBoardingStayCommand>
{
    public CancelBoardingStayCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(StayRules.MaxCancellationNoteLength).NoControlCharacters();
    }
}

public sealed class RecordBoardingNoShowCommandValidator : AbstractValidator<RecordBoardingNoShowCommand>
{
    public RecordBoardingNoShowCommandValidator() => RuleFor(x => x.StayId).NotEmpty();
}

using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordCatNeuterStatusCommandValidator : AbstractValidator<RecordCatNeuterStatusCommand>
{
    public RecordCatNeuterStatusCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.CatId).NotEmpty();

        RuleFor(x => x.NeuteredOn)
            .NotNull()
            .When(x => x.NeuterStatus == NeuterStatus.Neutered)
            .WithMessage("A neuter date is required when neuter status is Neutered.");

        RuleFor(x => x.NeuteredOn)
            .LessThanOrEqualTo(_ => CatRecordDateBound.LatestPossibleLocalDate(timeProvider))
            .When(x => x.NeuteredOn is not null)
            .WithMessage("The neuter date cannot be in the future.");
    }
}

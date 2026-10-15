using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-PHM-001 §11.2 -- shape only: ids non-empty, one to five lines with distinct batches and positive quantities, the witness fields both present or both absent. FEFO, stock,
// validity and whether a witness is needed at all are domain-service or handler rules. A validator NEVER echoes the password (U-X2).
// Registered as IValidator<T> (rule 21).

public sealed class DispensePrescriptionCommandValidator : AbstractValidator<DispensePrescriptionCommand>
{
    public DispensePrescriptionCommandValidator()
    {
        RuleFor(x => x.DispensingId).NotEmpty();
        RuleFor(x => x.PrescriptionId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Lines).NotNull().Must(l => l.Count is >= 1 and <= DispensingRules.MaxLinesPerDispensing)
            .WithMessage($"A supply is dispensed in one to {DispensingRules.MaxLinesPerDispensing} batches.");
        RuleFor(x => x.Lines).Must(l => l.Select(x => x.BatchId).Distinct().Count() == l.Count).WithMessage("A batch appears once.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.SkuId).NotEmpty();
            line.RuleFor(l => l.BatchId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0m).MaxDecimalPlaces(2);
        });
        RuleFor(x => x.FefoOverrideReason).MaximumLength(DispensingRules.MaxReasonLength).NoControlCharacters();
        RuleFor(x => x.RepeatRequestId).NotEqual(Guid.Empty).When(x => x.RepeatRequestId is not null);
        RuleFor(x => x.WitnessIdentifier).MaximumLength(200).WithMessage("The witness identifier is too long.");
        RuleFor(x => x.WitnessPassword).MaximumLength(1024).WithMessage("The witness password is too long.");
        RuleFor(x => x).Must(x => string.IsNullOrWhiteSpace(x.WitnessIdentifier) == string.IsNullOrWhiteSpace(x.WitnessPassword))
            .WithMessage("The witness identifier and password are given together.").WithName("Witness");
    }
}

public sealed class RecordLabelPrintCommandValidator : AbstractValidator<RecordLabelPrintCommand>
{
    public RecordLabelPrintCommandValidator()
    {
        RuleFor(x => x.DispensingId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class ReverseDispensingCommandValidator : AbstractValidator<ReverseDispensingCommand>
{
    public ReverseDispensingCommandValidator()
    {
        RuleFor(x => x.DispensingId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(DispensingRules.MaxReasonLength).NoControlCharacters();
        RuleFor(x => x.WitnessIdentifier).MaximumLength(200).WithMessage("The witness identifier is too long.");
        RuleFor(x => x.WitnessPassword).MaximumLength(1024).WithMessage("The witness password is too long.");
        RuleFor(x => x).Must(x => string.IsNullOrWhiteSpace(x.WitnessIdentifier) == string.IsNullOrWhiteSpace(x.WitnessPassword))
            .WithMessage("The witness identifier and password are given together.").WithName("Witness");
    }
}

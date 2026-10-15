using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-PHM-001 §11.2 -- shape only; the witness fields are both present or both absent, and a validator NEVER echoes the password (U-X2): every message here names
// the property, never the value. Registered as IValidator<T> (rule 21).

public sealed class RecordControlledDrugCorrectionCommandValidator : AbstractValidator<RecordControlledDrugCorrectionCommand>
{
    public RecordControlledDrugCorrectionCommandValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.CorrectsEntryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CorrectedDoseText).MaximumLength(ControlledDrugRules.MaxDoseTextLength).NoControlCharacters();
        RuleFor(x => x.CorrectedCatId).NotEqual(Guid.Empty).When(x => x.CorrectedCatId is not null);
        RuleFor(x => x.QuantityDelta).MaxDecimalPlaces(2);
        RuleFor(x => x.Notes).MaximumLength(ControlledDrugRules.MaxNotesLength).NoControlCharacters();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(ControlledDrugRules.MaxReasonLength).NoControlCharacters();
        RuleFor(x => x.WitnessIdentifier).NotEmpty().MaximumLength(200).WithMessage("The witness identifier is required.");
        RuleFor(x => x.WitnessPassword).NotEmpty().MaximumLength(1024).WithMessage("The witness password is required.");
    }
}

public sealed class SealControlledDrugEntriesCommandValidator : AbstractValidator<SealControlledDrugEntriesCommand>
{
    public SealControlledDrugEntriesCommandValidator() => RuleFor(x => x.BranchId).NotEmpty();
}

public sealed class RecordControlledDrugRegisterExportCommandValidator : AbstractValidator<RecordControlledDrugRegisterExportCommand>
{
    public RecordControlledDrugRegisterExportCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.RowCount).GreaterThanOrEqualTo(0);
    }
}

using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-SRG-001 §11.2 -- shape only: ids, dose greater than zero, quantities not negative with the wasted quantity not above the drawn quantity, enum membership, text lengths.
/// Surgery state, stock, the batch and the witness are handler rules; the plausibility of vitals is the aggregate's.</summary>
public sealed class LogAnaesthesiaDrugCommandValidator : AbstractValidator<LogAnaesthesiaDrugCommand>
{
    public LogAnaesthesiaDrugCommandValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.MedicineId).NotEmpty();
        RuleFor(x => x.AdministeredById).NotEmpty();
        RuleFor(x => x.DoseAmount).GreaterThan(0m).LessThanOrEqualTo(9_999_999m).ScalePrecision(3, 10);
        RuleFor(x => x.DoseUnit).MustBeValidEnumeration<LogAnaesthesiaDrugCommand, DoseUnit>();
        RuleFor(x => x.Route).MustBeValidEnumeration<LogAnaesthesiaDrugCommand, AdministrationRoute>();
        RuleFor(x => x.OccurredOn).Must(o => o != default);
        RuleFor(x => x.QuantityDrawn).GreaterThan(0m).When(x => x.QuantityDrawn is not null);
        RuleFor(x => x.QuantityWasted).GreaterThanOrEqualTo(0m).When(x => x.QuantityWasted is not null);
        RuleFor(x => x).Must(x => x.QuantityWasted is null || (x.QuantityDrawn is { } drawn && x.QuantityWasted <= drawn))
            .WithMessage("The quantity wasted cannot be more than the quantity drawn.").WithName(nameof(LogAnaesthesiaDrugCommand.QuantityWasted));
        RuleFor(x => x.WitnessIdentifier).MaximumLength(320);
        RuleFor(x => x.WitnessPassword).MaximumLength(256);
        RuleFor(x => x.FefoOverrideReason).MaximumLength(500).NoControlCharacters();
    }
}

public sealed class LogAnaesthesiaVitalsCommandValidator : AbstractValidator<LogAnaesthesiaVitalsCommand>
{
    public LogAnaesthesiaVitalsCommandValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.OccurredOn).Must(o => o != default);
        RuleFor(x => x.Note).MaximumLength(SurgeryRules.NoteMax).NoControlCharacters();
        // Whether at least one value is present, and whether the values are plausible, are the aggregate's rules (VitalsRequired, ImplausibleVitalValue).
    }
}

public sealed class LogAnaesthesiaMilestoneCommandValidator : AbstractValidator<LogAnaesthesiaMilestoneCommand>
{
    public LogAnaesthesiaMilestoneCommandValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Milestone).MustBeValidEnumeration<LogAnaesthesiaMilestoneCommand, AnaesthesiaMilestone>();
        RuleFor(x => x.OccurredOn).Must(o => o != default);
        RuleFor(x => x.Note).MaximumLength(SurgeryRules.NoteMax).NoControlCharacters();
    }
}

public sealed class CorrectAnaesthesiaEntryCommandValidator : AbstractValidator<CorrectAnaesthesiaEntryCommand>
{
    public CorrectAnaesthesiaEntryCommandValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CorrectsEntryId).NotEmpty();
        RuleFor(x => x.OccurredOn).Must(o => o != default);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(SurgeryRules.CorrectionReasonMax).NoControlCharacters();
        RuleFor(x => x.WitnessIdentifier).MaximumLength(320);
        RuleFor(x => x.WitnessPassword).MaximumLength(256);
        RuleFor(x => x.FefoOverrideReason).MaximumLength(500).NoControlCharacters();

        When(x => x.Drug is not null, () =>
        {
            RuleFor(x => x.Drug!.DoseAmount).GreaterThan(0m).LessThanOrEqualTo(9_999_999m).ScalePrecision(3, 10);
            RuleFor(x => x.Drug!.DoseUnit).MustBeValidEnumeration<CorrectAnaesthesiaEntryCommand, DoseUnit>();
            RuleFor(x => x.Drug!.Route).MustBeValidEnumeration<CorrectAnaesthesiaEntryCommand, AdministrationRoute>();
            RuleFor(x => x.Drug!.AdministeredById).NotEmpty();
            RuleFor(x => x.Drug!.QuantityDrawn).GreaterThan(0m).When(x => x.Drug!.QuantityDrawn is not null);
            RuleFor(x => x.Drug!.QuantityWasted).GreaterThanOrEqualTo(0m).When(x => x.Drug!.QuantityWasted is not null);
        });

        When(x => x.Milestone is not null, () =>
            RuleFor(x => x.Milestone!.Milestone).MustBeValidEnumeration<CorrectAnaesthesiaEntryCommand, AnaesthesiaMilestone>());

        When(x => x.Vitals is not null, () =>
            RuleFor(x => x.Vitals!.Note).MaximumLength(SurgeryRules.NoteMax).NoControlCharacters());
    }
}

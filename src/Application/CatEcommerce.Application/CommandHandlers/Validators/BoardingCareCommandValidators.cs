using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-BRD-001 section 11.2 (Phase E) -- shape only. Checklist completeness, signature validity and cut-offs are aggregate rules.

public sealed class CheckInBoardingStayCommandValidator : AbstractValidator<CheckInBoardingStayCommand>
{
    public CheckInBoardingStayCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Cats).NotNull().Must(c => c.Count is >= 1 and <= StayRules.MaxCatsPerStay).WithMessage("Check in between 1 and 6 cats.");
        RuleForEach(x => x.Cats).ChildRules(cat =>
        {
            cat.RuleFor(c => c.CatId).NotEmpty();
            cat.RuleFor(c => c.ArrivalHealthObservation).MaximumLength(1000).NoControlCharacters();
            cat.RuleFor(c => c.Belongings).Must(b => b.Count <= CareProfileRules.MaxBelongings).WithMessage($"At most {CareProfileRules.MaxBelongings} belongings.");
            cat.RuleForEach(c => c.Belongings).ChildRules(b =>
            {
                b.RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<BelongingItemInput, BelongingKind>();
                b.RuleFor(x => x.Description).NotEmpty().MaximumLength(CareProfileRules.MaxBelongingDescriptionLength).NoControlCharacters();
                b.RuleFor(x => x.FileIds).Must(f => f.Count <= CareProfileRules.MaxBelongingPhotos).WithMessage("At most 3 photos per item.");
            });
            cat.RuleFor(c => c.Signature).ChildRules(sig =>
            {
                sig.RuleFor(x => x!.SignerName).NotEmpty().MaximumLength(BoardingSignatureRules.MaxSignerNameLength).NoControlCharacters();
                sig.RuleFor(x => x!.Method).NotEmpty().MustBeValidEnumeration<SignatureInput, SignatureMethod>();
                sig.RuleFor(x => x!.Strokes).MaximumLength(BoardingSignatureRules.MaxSerialisedBytes);
            }).When(c => c.Signature is not null);
        });
    }
}

public sealed class GenerateCareTasksCommandValidator : AbstractValidator<GenerateCareTasksCommand>
{
    public GenerateCareTasksCommandValidator() => RuleFor(x => x.StayId).NotEmpty();
}

public sealed class CompleteCareTaskCommandValidator : AbstractValidator<CompleteCareTaskCommand>
{
    public CompleteCareTaskCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(IncidentRules.MaxTaskNoteLength).NoControlCharacters();
    }
}

public sealed class SkipCareTaskCommandValidator : AbstractValidator<SkipCareTaskCommand>
{
    public SkipCareTaskCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(IncidentRules.MaxSkipReasonLength).NoControlCharacters();
    }
}

public sealed class RecordCareObservationCommandValidator : AbstractValidator<RecordCareObservationCommand>
{
    public RecordCareObservationCommandValidator()
    {
        RuleFor(x => x.ObservationId).NotEmpty();
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.PortionEaten).NotEmpty().MustBeValidEnumeration<RecordCareObservationCommand, PortionEaten>();
        RuleFor(x => x.WaterIntake).NotEmpty().MustBeValidEnumeration<RecordCareObservationCommand, WaterIntake>();
        RuleFor(x => x.LitterOutput).NotEmpty().MustBeValidEnumeration<RecordCareObservationCommand, LitterOutput>();
        RuleFor(x => x.Mood).NotEmpty().MustBeValidEnumeration<RecordCareObservationCommand, CatMood>();
        RuleFor(x => x.Notes).MaximumLength(IncidentRules.MaxObservationNotesLength).NoControlCharacters();
    }
}

public sealed class FlagSuspectedInfectiousCommandValidator : AbstractValidator<FlagSuspectedInfectiousCommand>
{
    public FlagSuspectedInfectiousCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Note).NotEmpty().MaximumLength(StayRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class ClearSuspectedInfectiousCommandValidator : AbstractValidator<ClearSuspectedInfectiousCommand>
{
    public ClearSuspectedInfectiousCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(StayRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class RecordIsolationEntryCommandValidator : AbstractValidator<RecordIsolationEntryCommand>
{
    public RecordIsolationEntryCommandValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Purpose).NotEmpty().MaximumLength(IncidentRules.MaxIsolationPurposeLength).NoControlCharacters();
    }
}

public sealed class RecordIsolationExitCommandValidator : AbstractValidator<RecordIsolationExitCommand>
{
    public RecordIsolationExitCommandValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-SRG-001 §11.2 -- shape only: non-empty ids, a start with an offset, text lengths, enum membership. Availability, consent, qualification and transitions
/// are aggregate or handler rules (<c>.ai/command-handler-pattern.md</c> §6).</summary>
public sealed class BookSurgeryCommandValidator : AbstractValidator<BookSurgeryCommand>
{
    public BookSurgeryCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.TheatreId).NotEmpty();
        RuleFor(x => x.SurgeonId).NotEmpty();
        RuleFor(x => x.AssistantId).NotEmpty();
        RuleFor(x => x.StartsOn).Must(s => s != default);
        RuleFor(x => x.CampaignEligibilityNote).MaximumLength(SurgeryRules.EligibilityNoteMax).NoControlCharacters();
    }
}

public sealed class RescheduleSurgeryCommandValidator : AbstractValidator<RescheduleSurgeryCommand>
{
    public RescheduleSurgeryCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StartsOn).Must(s => s != default);
        RuleFor(x => x.TheatreId).NotEmpty();
        RuleFor(x => x.SurgeonId).NotEmpty();
        RuleFor(x => x.AssistantId).NotEmpty();
    }
}

public sealed class CancelSurgeryCommandValidator : AbstractValidator<CancelSurgeryCommand>
{
    public CancelSurgeryCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithErrorCode(BusinessViolationCodes.CancellationReasonRequired).WithMessage("A reason is required to cancel a surgery.");
        RuleFor(x => x.Reason).MaximumLength(SurgeryRules.CancellationReasonMax).NoControlCharacters();
    }
}

public sealed class CompleteChecklistItemCommandValidator : AbstractValidator<CompleteChecklistItemCommand>
{
    public CompleteChecklistItemCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Kind).MustBeValidEnumeration<CompleteChecklistItemCommand, PreOpChecklistItemKind>();
        RuleFor(x => x.Note).MaximumLength(SurgeryRules.NoteMax).NoControlCharacters();
    }
}

public sealed class WaiveChecklistItemCommandValidator : AbstractValidator<WaiveChecklistItemCommand>
{
    public WaiveChecklistItemCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Kind).MustBeValidEnumeration<WaiveChecklistItemCommand, PreOpChecklistItemKind>();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(SurgeryRules.WaiverReasonMax).NoControlCharacters();
    }
}

public sealed class RecordAsaGradeCommandValidator : AbstractValidator<RecordAsaGradeCommand>
{
    public RecordAsaGradeCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Grade).MustBeValidEnumeration<RecordAsaGradeCommand, AsaGrade>();
    }
}

public sealed class AdmitSurgeryCommandValidator : AbstractValidator<AdmitSurgeryCommand>
{
    public AdmitSurgeryCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class StartSurgeryCommandValidator : AbstractValidator<StartSurgeryCommand>
{
    public StartSurgeryCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        // 10 to 1000 is an aggregate rule (ConsentOverrideReasonRequired); the shape rule only bounds the upper end and refuses control characters.
        RuleFor(x => x.ConsentOverrideReason).MaximumLength(SurgeryRules.OverrideReasonMax + 200).NoControlCharacters();
    }
}

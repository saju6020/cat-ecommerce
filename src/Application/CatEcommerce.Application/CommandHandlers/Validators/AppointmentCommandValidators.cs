using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-APT-001 §11.2 -- shape only. Availability, cut-off, grid and qualification are
/// aggregate or handler rules.</summary>
public sealed class PlaceSlotHoldCommandValidator : AbstractValidator<PlaceSlotHoldCommand>
{
    public PlaceSlotHoldCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
    }
}

public sealed class ReleaseSlotHoldCommandValidator : AbstractValidator<ReleaseSlotHoldCommand>
{
    public ReleaseSlotHoldCommandValidator() => RuleFor(x => x.AppointmentId).NotEmpty();
}

public sealed class ConfirmAppointmentCommandValidator : AbstractValidator<ConfirmAppointmentCommand>
{
    public ConfirmAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.ReasonForVisit).MaximumLength(AppointmentRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class RescheduleAppointmentCommandValidator : AbstractValidator<RescheduleAppointmentCommand>
{
    public RescheduleAppointmentCommandValidator() => RuleFor(x => x.AppointmentId).NotEmpty();
}

public sealed class CancelAppointmentCommandValidator : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(AppointmentRules.MaxReasonLength).NoControlCharacters();

        // §11.4 C5: a reason is required when staff cancels (the customer's own cancel needs none).
        RuleFor(x => x.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithErrorCode(BusinessViolationCodes.CancellationReasonRequired)
            .WithMessage("A reason is required when staff cancels an appointment.")
            .When(x => x.IsStaff);
    }
}

public sealed class BookAppointmentByStaffCommandValidator : AbstractValidator<BookAppointmentByStaffCommand>
{
    public BookAppointmentByStaffCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.PerformerId).NotEmpty();
        RuleFor(x => x.ReasonForVisit).MaximumLength(AppointmentRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class MoveAppointmentCommandValidator : AbstractValidator<MoveAppointmentCommand>
{
    public MoveAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.PerformerId).NotEmpty();
        RuleFor(x => x.RoomId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public sealed class RegisterWalkInCommandValidator : AbstractValidator<RegisterWalkInCommand>
{
    public RegisterWalkInCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.TriagePriority).NotEmpty().MustBeValidEnumeration<RegisterWalkInCommand, TriagePriority>();
        RuleFor(x => x.PresentingComplaint).MaximumLength(AppointmentRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class CheckInAppointmentCommandValidator : AbstractValidator<CheckInAppointmentCommand>
{
    public CheckInAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.TriagePriority).MustBeValidEnumeration<CheckInAppointmentCommand, TriagePriority>()
            .When(x => x.TriagePriority is not null);
    }
}

public sealed class SetTriagePriorityCommandValidator : AbstractValidator<SetTriagePriorityCommand>
{
    public SetTriagePriorityCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.TriagePriority).NotEmpty().MustBeValidEnumeration<SetTriagePriorityCommand, TriagePriority>();
    }
}

public sealed class StartAppointmentCommandValidator : AbstractValidator<StartAppointmentCommand>
{
    public StartAppointmentCommandValidator() => RuleFor(x => x.AppointmentId).NotEmpty();
}

public sealed class CompleteAppointmentCommandValidator : AbstractValidator<CompleteAppointmentCommand>
{
    public CompleteAppointmentCommandValidator() => RuleFor(x => x.AppointmentId).NotEmpty();
}

public sealed class RecordNoShowCommandValidator : AbstractValidator<RecordNoShowCommand>
{
    public RecordNoShowCommandValidator() => RuleFor(x => x.AppointmentId).NotEmpty();
}

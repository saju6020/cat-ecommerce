using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class ReferStayCatToClinicCommandValidator : AbstractValidator<ReferStayCatToClinicCommand>
{
    public ReferStayCatToClinicCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(IncidentRules.MaxReferralReasonLength).NoControlCharacters();
        RuleFor(x => x.Urgency).NotEmpty().MustBeValidEnumeration<ReferStayCatToClinicCommand, TriagePriority>();
    }
}

public sealed class RecordReferralReturnCommandValidator : AbstractValidator<RecordReferralReturnCommand>
{
    public RecordReferralReturnCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.ReferralId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class RequestVetChargeApprovalCommandValidator : AbstractValidator<RequestVetChargeApprovalCommand>
{
    public RequestVetChargeApprovalCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m).LessThanOrEqualTo(BillingRules.MaxAmount);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(IncidentRules.MaxVetApprovalDescriptionLength).NoControlCharacters();
    }
}

public sealed class RespondToVetChargeApprovalCommandValidator : AbstractValidator<RespondToVetChargeApprovalCommand>
{
    public RespondToVetChargeApprovalCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.ApprovalId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(IncidentRules.MaxApprovalNoteLength).NoControlCharacters();
    }
}

public sealed class RecordVetChargeApprovalByStaffCommandValidator : AbstractValidator<RecordVetChargeApprovalByStaffCommand>
{
    public RecordVetChargeApprovalByStaffCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.ApprovalId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Note).NotEmpty().MaximumLength(IncidentRules.MaxApprovalNoteLength).NoControlCharacters();
    }
}

public sealed class ExtendBoardingStayCommandValidator : AbstractValidator<ExtendBoardingStayCommand>
{
    public ExtendBoardingStayCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public sealed class RelocateBoardingStayCommandValidator : AbstractValidator<RelocateBoardingStayCommand>
{
    public RelocateBoardingStayCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.TargetUnitId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(StayRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class CheckOutBoardingStayCommandValidator : AbstractValidator<CheckOutBoardingStayCommand>
{
    public CheckOutBoardingStayCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Cats).NotNull().Must(c => c is { Count: >= 1 and <= StayRules.MaxCatsPerStay }).WithMessage($"Give the check-out of each cat (1 to {StayRules.MaxCatsPerStay}).");
        RuleForEach(x => x.Cats).ChildRules(cat =>
        {
            cat.RuleFor(c => c.CatId).NotEmpty();
            cat.RuleFor(c => c.Belongings).NotNull().Must(b => b is null || b.Count <= CareProfileRules.MaxBelongings);
            cat.RuleForEach(c => c.Belongings).ChildRules(item =>
            {
                item.RuleFor(i => i.BelongingId).NotEmpty();
                item.RuleFor(i => i.Note).MaximumLength(CareProfileRules.MaxBelongingDescriptionLength).NoControlCharacters();
            });
        });
    }
}

public sealed class ClearStayAttentionCommandValidator : AbstractValidator<ClearStayAttentionCommand>
{
    public ClearStayAttentionCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Note).NotEmpty().MaximumLength(StayRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class InvoiceBoardingCycleCommandValidator : AbstractValidator<InvoiceBoardingCycleCommand>
{
    public InvoiceBoardingCycleCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.Cycle).GreaterThanOrEqualTo(0); // 0 = the final invoice
    }
}

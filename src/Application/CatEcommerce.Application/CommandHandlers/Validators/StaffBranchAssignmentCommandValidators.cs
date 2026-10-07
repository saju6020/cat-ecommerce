using CatEcommerce.Application.Commands.Administration;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class AssignStaffBranchesCommandValidator : AbstractValidator<AssignStaffBranchesCommand>
{
    public AssignStaffBranchesCommandValidator()
    {
        RuleFor(x => x.StaffAccountId).NotEmpty();
        RuleFor(x => x.BranchIds).NotEmpty();
        RuleFor(x => x.PrimaryBranchId).NotEmpty();
    }
}

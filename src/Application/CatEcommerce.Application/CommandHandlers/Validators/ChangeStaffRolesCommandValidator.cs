using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class ChangeStaffRolesCommandValidator : AbstractValidator<ChangeStaffRolesCommand>
{
    public ChangeStaffRolesCommandValidator()
    {
        RuleFor(x => x.StaffAccountId).NotEmpty();
        RuleFor(x => x.RoleIds).NotEmpty().WithErrorCode(BusinessViolationCodes.StaffRolesRequired);
    }
}

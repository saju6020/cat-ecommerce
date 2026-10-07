using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Application.DataMappers;

public static class StaffAccountMapper
{
    public static StaffAccountCreationDto ToCreationDto(
        this CreateStaffAccountCommand command, MobileNumber? mobile, EmailAddress? email) =>
        new(command.FullName, mobile, email, command.RoleIds, command.UserContext, command.CorrelationId);

    public static StaffRoleChangeDto ToRoleChangeDto(this ChangeStaffRolesCommand command) =>
        new(command.RoleIds, command.UserContext, command.CorrelationId);
}

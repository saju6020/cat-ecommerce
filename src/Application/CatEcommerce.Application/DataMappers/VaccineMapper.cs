using CatEcommerce.Application.Commands.Vaccination;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Application.DataMappers;

public static class VaccineMapper
{
    public static VaccineRegistrationDto ToDomainDto(this RegisterVaccineCommand command) =>
        new(command.Code, command.Name, command.IsCore, command.ValidityMonths, command.Description,
            command.UserContext, command.CorrelationId);
}

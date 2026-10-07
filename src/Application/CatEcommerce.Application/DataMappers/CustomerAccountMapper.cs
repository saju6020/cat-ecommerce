using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Application.DataMappers;

/// <summary>
/// HO-ACC-001 §11.3. Takes already-constructed value objects -- the handler builds them first so
/// a factory failure becomes a 400 before any DTO exists. Always passes UserContext/CorrelationId
/// through. No mapper maps a plaintext secret into a DTO an event can reach.
/// </summary>
public static class CustomerAccountMapper
{
    public static CustomerRegistrationDto ToRegistrationDto(
        this RegisterCustomerCommand command, MobileNumber? mobile, EmailAddress? email, PasswordCredential credential) =>
        new(command.FullName, mobile, email, credential, command.UserContext, command.CorrelationId);

    public static WalkInCustomerRegistrationDto ToWalkInRegistrationDto(
        this RegisterWalkInCustomerCommand command, MobileNumber mobile) =>
        new(command.FullName, mobile, command.UserContext, command.CorrelationId);

    public static CustomerContactDetailsDto ToContactDetailsDto(
        this UpdateCustomerContactDetailsCommand command, EmailAddress? email, IReadOnlyList<MobileNumber> additionalNumbers) =>
        new(command.FullName, email, additionalNumbers, command.UserContext, command.CorrelationId);

    public static CustomerMergeDto ToMergeDto(
        this MergeCustomerCommand command, MobileNumber? survivorMobile, IReadOnlyList<string> survivorAdditionalNumbers,
        EmailAddress? survivorEmail, DateTimeOffset asOf) =>
        new(command.SurvivingCustomerId, command.Reason, survivorMobile, survivorAdditionalNumbers, survivorEmail, asOf, command.UserContext, command.CorrelationId);

    public static CustomerAnonymisationDto ToAnonymisationDto(this DeleteCustomerAccountCommand command, DateTimeOffset asOf) =>
        new(asOf, command.UserContext, command.CorrelationId);

    public static PersonalDataExportDto ToExportDto(this RecordPersonalDataExportCommand command, DateTimeOffset asOf) =>
        new(asOf, command.UserContext, command.CorrelationId);
}

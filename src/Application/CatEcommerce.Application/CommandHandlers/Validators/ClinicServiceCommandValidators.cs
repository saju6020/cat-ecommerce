using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class CreateClinicServiceCommandValidator : AbstractValidator<CreateClinicServiceCommand>
{
    public CreateClinicServiceCommandValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        // Gate fix 2026-09-26 (SEC LOW): a format check, before the handler's own uniqueness
        // pre-check query runs; Name/Description get NoControlCharacters as free text.
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30).MustBeCodeFormat();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.Description).MaximumLength(2000).NoControlCharacters();
        // Gate fix 2026-09-26 (SEC LOW, hardening round): unknown/wrongly-cased -> 400, not a 500.
        RuleFor(x => x.Category).NotEmpty().MustBeValidEnumeration<CreateClinicServiceCommand, ServiceCategory>();
        RuleFor(x => x.QualifiedRoleIds).NotEmpty();
        RuleFor(x => x.RequiredRoomKind).NotEmpty().MustBeValidEnumeration<CreateClinicServiceCommand, RoomKind>();
        RuleFor(x => x.TaxClassId).NotEmpty();
    }
}

public sealed class UpdateClinicServiceCommandValidator : AbstractValidator<UpdateClinicServiceCommand>
{
    public UpdateClinicServiceCommandValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.Description).MaximumLength(2000).NoControlCharacters();
        RuleFor(x => x.Category).NotEmpty().MustBeValidEnumeration<UpdateClinicServiceCommand, ServiceCategory>();
        RuleFor(x => x.QualifiedRoleIds).NotEmpty();
        RuleFor(x => x.RequiredRoomKind).NotEmpty().MustBeValidEnumeration<UpdateClinicServiceCommand, RoomKind>();
        RuleFor(x => x.TaxClassId).NotEmpty();
    }
}

public sealed class SetClinicServiceActivationCommandValidator : AbstractValidator<SetClinicServiceActivationCommand>
{
    public SetClinicServiceActivationCommandValidator() => RuleFor(x => x.ServiceId).NotEmpty();
}

public sealed class SetServicePriceCommandValidator : AbstractValidator<SetServicePriceCommand>
{
    public SetServicePriceCommandValidator()
    {
        RuleFor(x => x.PriceId).NotEmpty();
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
    }
}

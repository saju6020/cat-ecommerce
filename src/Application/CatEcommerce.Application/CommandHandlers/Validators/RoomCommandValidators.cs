using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
{
    public CreateRoomCommandValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        // Gate fix 2026-09-26 (SEC LOW): a NUL byte previously reached Postgres unfiltered.
        RuleFor(x => x.Name).NotEmpty().MaximumLength(BranchRules.RoomNameMaxLength).NoControlCharacters();
        // Gate fix 2026-09-26 (SEC LOW, hardening round): unknown/wrongly-cased -> 400, not a 500
        // out of AdministrationMapper's Enumeration.FromDisplayName.
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<CreateRoomCommand, RoomKind>();
    }
}

public sealed class UpdateRoomCommandValidator : AbstractValidator<UpdateRoomCommand>
{
    public UpdateRoomCommandValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(BranchRules.RoomNameMaxLength).NoControlCharacters();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<UpdateRoomCommand, RoomKind>();
    }
}

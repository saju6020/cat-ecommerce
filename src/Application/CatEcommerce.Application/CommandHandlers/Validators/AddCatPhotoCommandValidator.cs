using CatEcommerce.Application.Commands.Cat;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>
/// Content type and size are no longer client-supplied (HO-FIL-001 §1 Amended) -- the handler
/// reads them from the verified <c>StoredFileAggregate</c> row and <c>CatAggregate.AddPhoto</c>
/// itself still enforces <c>CatPhotoRules</c> against those server-read values (defence in depth).
/// </summary>
public sealed class AddCatPhotoCommandValidator : AbstractValidator<AddCatPhotoCommand>
{
    public AddCatPhotoCommandValidator()
    {
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.PhotoId).NotEmpty();
        RuleFor(x => x.FileId).NotEmpty();
    }
}

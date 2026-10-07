using CatEcommerce.Application.Commands.StoredFile;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class DeleteStoredFileCommandValidator : AbstractValidator<DeleteStoredFileCommand>
{
    public DeleteStoredFileCommandValidator()
    {
        RuleFor(x => x.FileId).NotEmpty();
    }
}

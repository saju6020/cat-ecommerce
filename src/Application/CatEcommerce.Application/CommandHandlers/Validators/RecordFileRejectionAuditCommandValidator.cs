using CatEcommerce.Application.Commands.StoredFile;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordFileRejectionAuditCommandValidator : AbstractValidator<RecordFileRejectionAuditCommand>
{
    public RecordFileRejectionAuditCommandValidator()
    {
        RuleFor(x => x.AttemptedFileId).NotEmpty();
        RuleFor(x => x.ViolationCode).NotEmpty();
        RuleFor(x => x.Purpose).NotEmpty();
    }
}

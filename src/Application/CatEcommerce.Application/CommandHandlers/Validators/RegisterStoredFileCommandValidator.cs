using System.Text.RegularExpressions;
using CatEcommerce.Application.Commands.StoredFile;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed partial class RegisterStoredFileCommandValidator : AbstractValidator<RegisterStoredFileCommand>
{
    public RegisterStoredFileCommandValidator()
    {
        RuleFor(x => x.FileId).NotEmpty();
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SizeInBytes).GreaterThan(0);
        RuleFor(x => x.Sha256).NotEmpty().Must(BeAHexSha256).WithMessage("Sha256 must be 64 hex characters.");
        RuleFor(x => x.OriginalFileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.StorageKey).NotEmpty().MaximumLength(500);
        RuleFor(x => x.UploadedBy).NotEmpty();
        RuleFor(x => x.ScannerName).NotEmpty().MaximumLength(200);
    }

    private static bool BeAHexSha256(string value) => Sha256Pattern().IsMatch(value);

    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex Sha256Pattern();
}

using CatEcommerce.Application.Commands.Operations;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordOfflineSnapshotIssuedCommandValidator : AbstractValidator<RecordOfflineSnapshotIssuedCommand>
{
    public RecordOfflineSnapshotIssuedCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ETag).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SectionCounts).NotNull().Must(c => c.Count <= 10).WithMessage("At most ten sections.");
    }
}

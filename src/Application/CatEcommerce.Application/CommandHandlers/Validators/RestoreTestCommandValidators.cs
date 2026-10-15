using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-XCT-001 section 11.2 -- shape only. Time ordering, "not in the future" and the computed outcome are aggregate rules.
public sealed class RecordRestoreTestCommandValidator : AbstractValidator<RecordRestoreTestCommand>
{
    public RecordRestoreTestCommandValidator()
    {
        RuleFor(x => x.RestoreTestId).NotEmpty();
        RuleFor(x => x.Environment).NotEmpty().MaximumLength(RestoreTestRecordAggregate.MaxEnvironmentLength).NoControlCharacters();
        RuleFor(x => x.BackupSetLabel).NotEmpty().MaximumLength(RestoreTestRecordAggregate.MaxBackupSetLabelLength).NoControlCharacters();
        RuleFor(x => x.DrillOutput).NotEmpty().MaximumLength(RestoreTestRecordAggregate.MaxDrillOutputLength);
        RuleFor(x => x.Notes).MaximumLength(RestoreTestRecordAggregate.MaxNotesLength);

        RuleFor(x => x.Checks).NotNull()
            .Must(c => c is { Count: > 0 }).WithErrorCode(BusinessViolationCodes.RestoreTestChecksRequired).WithMessage(OperationsMessages.RestoreTestChecksRequired)
            .Must(c => c is null || c.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == c.Count).WithMessage("Check names must be different.");
        RuleForEach(x => x.Checks).ChildRules(check =>
        {
            check.RuleFor(c => c.Name).NotEmpty().MaximumLength(RestoreCheckResult.MaxNameLength).NoControlCharacters();
            check.RuleFor(c => c.Detail).MaximumLength(RestoreCheckResult.MaxDetailLength);
        }).When(x => x.Checks is not null);
    }
}

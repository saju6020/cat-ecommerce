using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-XCT-001 section 11.2 -- shape only. Run-state transitions are aggregate rules.

public sealed class RequestRetentionRunCommandValidator : AbstractValidator<RequestRetentionRunCommand>
{
    public RequestRetentionRunCommandValidator()
    {
        RuleFor(x => x.RetentionRunId).NotEmpty();
        RuleFor(x => x.Trigger).MustBeValidEnumeration<RequestRetentionRunCommand, RetentionRunTrigger>();
    }
}

public sealed class StartRetentionRunCommandValidator : AbstractValidator<StartRetentionRunCommand>
{
    public StartRetentionRunCommandValidator() => RuleFor(x => x.RetentionRunId).NotEmpty();
}

public sealed class RecordRetentionPassCommandValidator : AbstractValidator<RecordRetentionPassCommand>
{
    public RecordRetentionPassCommandValidator()
    {
        RuleFor(x => x.RetentionRunId).NotEmpty();
        RuleFor(x => x.PassKind).MustBeValidEnumeration<RecordRetentionPassCommand, RetentionPassKind>();
        RuleFor(x => x.Examined).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Affected).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Held).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Failed).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DurationMs).GreaterThanOrEqualTo(0);
    }
}

public sealed class FinishRetentionRunCommandValidator : AbstractValidator<FinishRetentionRunCommand>
{
    public FinishRetentionRunCommandValidator() => RuleFor(x => x.RetentionRunId).NotEmpty();
}

public sealed class PurgeExpiredRecordsCommandValidator : AbstractValidator<PurgeExpiredRecordsCommand>
{
    public PurgeExpiredRecordsCommandValidator()
    {
        RuleFor(x => x.PassKind).MustBeValidEnumeration<PurgeExpiredRecordsCommand, RetentionPassKind>();
        RuleFor(x => x.BatchSize).InclusiveBetween(1, RetentionRules.BatchSize);
    }
}

public sealed class RunPurgeSourceCommandValidator : AbstractValidator<RunPurgeSourceCommand>
{
    public RunPurgeSourceCommandValidator()
    {
        RuleFor(x => x.SourceKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BatchSize).InclusiveBetween(1, RetentionRules.BatchSize);
    }
}

public sealed class RedactExpiredNotificationContentCommandValidator : AbstractValidator<RedactExpiredNotificationContentCommand>
{
    public RedactExpiredNotificationContentCommandValidator() =>
        RuleFor(x => x).Must(x => (x.NotificationId is null) != (x.RequestId is null))
            .WithMessage("Exactly one of NotificationId or RequestId is required.");
}

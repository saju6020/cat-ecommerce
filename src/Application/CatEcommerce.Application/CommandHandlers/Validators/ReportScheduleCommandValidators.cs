using CatEcommerce.Application.Commands.Reporting;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-RPT-001 section 11.2 -- shape only. Catalogue membership, parameter validity, recipient permission and branch assignment are handler rules; the aggregate owns the rest of its own invariants.

/// <summary>The rules C2 and C3 share.</summary>
internal static class ReportScheduleShape
{
    public static void Apply<T>(AbstractValidator<T> validator) where T : ReportScheduleCommandBase
    {
        validator.RuleFor(x => x.ScheduleId).NotEmpty();
        validator.RuleFor(x => x.Name).NotEmpty().MaximumLength(ReportScheduleRules.NameMaxLength);
        validator.RuleFor(x => x.ReportKey).NotEmpty().MaximumLength(ReportRules.KeyMaxLength);
        validator.RuleFor(x => x.Period).NotNull().Must(p => Enumeration.GetAll<ReportRelativePeriod>().Contains(p)).WithMessage("Choose one of the listed periods.");
        validator.RuleFor(x => x.Format).NotNull().Must(f => f == ReportFormat.Csv || f == ReportFormat.Pdf).WithMessage("A schedule sends a CSV or a PDF.");
        validator.RuleFor(x => x.Frequency).NotNull().Must(f => Enumeration.GetAll<ReportFrequency>().Contains(f)).WithMessage("Choose daily, weekly or monthly.");
        validator.RuleFor(x => x.SendAtLocal).Must(t => t.Second == 0 && t.Millisecond == 0 && t.Minute % ReportScheduleRules.SendAtStepMinutes == 0)
            .WithMessage($"The send time must be on a {ReportScheduleRules.SendAtStepMinutes}-minute step.");
        validator.RuleFor(x => x.DayOfMonth).InclusiveBetween(1, ReportScheduleRules.MaxDayOfMonth).When(x => x.DayOfMonth is not null);
        validator.RuleFor(x => x.RecipientIds).NotNull().Must(r => r.Count >= 1 && r.Count <= ReportScheduleRules.MaxRecipients).WithMessage($"Choose between 1 and {ReportScheduleRules.MaxRecipients} recipients.")
            .Must(r => r.Distinct().Count() == r.Count).WithMessage("Each recipient can be listed once.");
        validator.RuleForEach(x => x.RecipientIds).NotEmpty();
        validator.RuleFor(x => x.Parameters).NotNull();
    }
}

public sealed class CreateReportScheduleCommandValidator : AbstractValidator<CreateReportScheduleCommand>
{
    public CreateReportScheduleCommandValidator() => ReportScheduleShape.Apply(this);
}

public sealed class UpdateReportScheduleCommandValidator : AbstractValidator<UpdateReportScheduleCommand>
{
    public UpdateReportScheduleCommandValidator()
    {
        ReportScheduleShape.Apply(this);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public sealed class PauseReportScheduleCommandValidator : AbstractValidator<PauseReportScheduleCommand>
{
    public PauseReportScheduleCommandValidator() => RuleFor(x => x.ScheduleId).NotEmpty();
}

public sealed class ResumeReportScheduleCommandValidator : AbstractValidator<ResumeReportScheduleCommand>
{
    public ResumeReportScheduleCommandValidator() => RuleFor(x => x.ScheduleId).NotEmpty();
}

public sealed class CancelReportScheduleCommandValidator : AbstractValidator<CancelReportScheduleCommand>
{
    public CancelReportScheduleCommandValidator() => RuleFor(x => x.ScheduleId).NotEmpty();
}

public sealed class AdvanceReportScheduleCommandValidator : AbstractValidator<AdvanceReportScheduleCommand>
{
    public AdvanceReportScheduleCommandValidator()
    {
        RuleFor(x => x.ScheduleId).NotEmpty();
        RuleFor(x => x.DueOn).NotEmpty();
    }
}

public sealed class RecordScheduledReportRunCommandValidator : AbstractValidator<RecordScheduledReportRunCommand>
{
    public RecordScheduledReportRunCommandValidator()
    {
        RuleFor(x => x.RunId).NotEmpty();
        RuleFor(x => x.ScheduleId).NotEmpty();
        RuleFor(x => x.DueOn).NotEmpty();
        RuleFor(x => x.Trigger).NotNull();
        RuleFor(x => x.PeriodTo).GreaterThanOrEqualTo(x => x.PeriodFrom);
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Format).NotNull();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(200).Matches(ReportScheduleRules.FileNamePattern);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ContentLength).GreaterThan(0);
        RuleFor(x => x.ContentSha256).NotEmpty().Length(64);
        RuleFor(x => x.FileId).NotNull().When(x => !x.AttachmentOmitted).WithMessage("A report file that is attached needs its stored file.");
        RuleFor(x => x.RowCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ColumnKeys).NotNull();
    }
}

public sealed class RecordFailedReportRunCommandValidator : AbstractValidator<RecordFailedReportRunCommand>
{
    public RecordFailedReportRunCommandValidator()
    {
        RuleFor(x => x.RunId).NotEmpty();
        RuleFor(x => x.ScheduleId).NotEmpty();
        RuleFor(x => x.DueOn).NotEmpty();
        RuleFor(x => x.Trigger).NotNull();
        RuleFor(x => x.FailureCode).NotEmpty().MaximumLength(ReportScheduleRules.FailureCodeMaxLength);
    }
}

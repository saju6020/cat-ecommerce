using CatEcommerce.Application.Commands.Reporting;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>Shape only (HO-RPT-001 section 11.2); catalogue membership and parameter validity are the pipeline's rules.</summary>
public sealed class RecordReportExportCommandValidator : AbstractValidator<RecordReportExportCommand>
{
    public RecordReportExportCommandValidator()
    {
        RuleFor(x => x.ReportKey).NotEmpty().MaximumLength(ReportRules.KeyMaxLength).Matches(ReportRules.KeyPattern);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
        RuleFor(x => x.RowCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ColumnKeys).NotEmpty();
    }
}
